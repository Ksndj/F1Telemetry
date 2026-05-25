using System.Collections.Concurrent;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using F1Telemetry.Core.Models;

namespace F1Telemetry.App.Logging;

/// <summary>
/// Writes JSON-line records on a bounded background queue.
/// </summary>
internal sealed class AsyncJsonLineLogWriter<TRecord> : IAsyncDisposable, IDisposable
{
    private const int QueueCapacity = 4096;
    private const int DefaultFlushTimeoutMilliseconds = 1500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentQueue<TRecord> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Func<string> _directoryPathFactory;
    private readonly Func<LogSettings, bool> _enabledSelector;
    private readonly string _filePrefix;
    private readonly string _fileExtension;
    private readonly Task _worker;
    private readonly object _statusGate = new();
    private LogSettings _settings = new();
    private bool _stopping;
    private int _queuedCount;
    private int _activeWriteCount;
    private StreamWriter? _writer;
    private DateOnly? _writerDate;
    private string _currentFilePath = string.Empty;
    private string _lastWarning = string.Empty;
    private long _writtenCount;
    private long _droppedCount;

    /// <summary>
    /// Initializes a new asynchronous JSON-line writer.
    /// </summary>
    public AsyncJsonLineLogWriter(
        Func<string> directoryPathFactory,
        Func<LogSettings, bool> enabledSelector,
        string filePrefix,
        string fileExtension)
    {
        _directoryPathFactory = directoryPathFactory ?? throw new ArgumentNullException(nameof(directoryPathFactory));
        _enabledSelector = enabledSelector ?? throw new ArgumentNullException(nameof(enabledSelector));
        _filePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "log" : filePrefix.Trim();
        _fileExtension = string.IsNullOrWhiteSpace(fileExtension) ? ".jsonl" : fileExtension.Trim();
        _worker = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Gets the current writer status.
    /// </summary>
    public LogWriterStatus Status
    {
        get
        {
            lock (_statusGate)
            {
                return new LogWriterStatus
                {
                    Enabled = _enabledSelector(_settings),
                    DirectoryPath = _directoryPathFactory(),
                    CurrentFilePath = _currentFilePath,
                    WrittenCount = Interlocked.Read(ref _writtenCount),
                    DroppedCount = Interlocked.Read(ref _droppedCount),
                    LastWarning = _lastWarning
                };
            }
        }
    }

    /// <summary>
    /// Updates writer settings used for future records.
    /// </summary>
    public void UpdateSettings(LogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_statusGate)
        {
            _settings = Normalize(settings);
            if (_enabledSelector(_settings))
            {
                _lastWarning = string.Empty;
            }
        }
    }

    /// <summary>
    /// Enqueues one record without waiting for file I/O.
    /// </summary>
    public bool TryEnqueue(TRecord record)
    {
        if (record is null || !IsEnabled())
        {
            return false;
        }

        var queuedCount = Interlocked.Increment(ref _queuedCount);
        if (queuedCount > QueueCapacity)
        {
            Interlocked.Decrement(ref _queuedCount);
            Interlocked.Increment(ref _droppedCount);
            SetWarning("日志队列已满，已丢弃一条记录。");
            return false;
        }

        _queue.Enqueue(record);
        _signal.Release();
        return true;
    }

    /// <summary>
    /// Waits briefly for pending records to be written.
    /// </summary>
    public async Task FlushAsync(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMilliseconds(DefaultFlushTimeoutMilliseconds);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;
        _signal.Release();

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (Volatile.Read(ref _queuedCount) == 0 && Volatile.Read(ref _activeWriteCount) == 0)
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        SetWarning("日志 flush 超时，已继续关闭流程。");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_statusGate)
        {
            _stopping = true;
        }

        await FlushAsync().ConfigureAwait(false);
        _signal.Release();
        var completed = await Task.WhenAny(_worker, Task.Delay(DefaultFlushTimeoutMilliseconds)).ConfigureAwait(false);
        if (completed != _worker)
        {
            SetWarning("日志后台线程关闭超时。");
        }

        await CloseWriterAsync().ConfigureAwait(false);
        _signal.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            await _signal.WaitAsync().ConfigureAwait(false);

            while (_queue.TryDequeue(out var record))
            {
                Interlocked.Decrement(ref _queuedCount);
                Interlocked.Increment(ref _activeWriteCount);
                try
                {
                    await WriteRecordAsync(record).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeWriteCount);
                }
            }

            if (IsStopping() && _queue.IsEmpty)
            {
                await CloseWriterAsync().ConfigureAwait(false);
                return;
            }
        }
    }

    private async Task WriteRecordAsync(TRecord record)
    {
        try
        {
            var writer = EnsureWriter();
            var line = JsonSerializer.Serialize(record, JsonOptions);
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            Interlocked.Increment(ref _writtenCount);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _droppedCount);
            SetWarning($"日志写入失败：{ex.Message}");
        }
    }

    private StreamWriter EnsureWriter()
    {
        var now = DateTimeOffset.Now;
        var writerDate = DateOnly.FromDateTime(now.DateTime);
        var currentPath = string.Empty;
        lock (_statusGate)
        {
            currentPath = _currentFilePath;
        }

        if (_writer is not null &&
            _writerDate == writerDate &&
            !IsAtOrAboveSizeLimit(currentPath))
        {
            return _writer;
        }

        CloseWriterAsync().GetAwaiter().GetResult();

        var directoryPath = _directoryPathFactory();
        Directory.CreateDirectory(directoryPath);
        CleanupOldFiles(directoryPath, now);

        var filePath = ResolveWritableFilePath(directoryPath, writerDate);
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream);
        _writerDate = writerDate;
        lock (_statusGate)
        {
            _currentFilePath = filePath;
        }

        return _writer;
    }

    private string ResolveWritableFilePath(string directoryPath, DateOnly writerDate)
    {
        var baseName = $"{_filePrefix}-{writerDate:yyyyMMdd}";
        var firstPath = Path.Combine(directoryPath, baseName + _fileExtension);
        if (!IsAtOrAboveSizeLimit(firstPath))
        {
            return firstPath;
        }

        for (var index = 1; index < 1000; index++)
        {
            var candidate = Path.Combine(directoryPath, $"{baseName}-{index:000}{_fileExtension}");
            if (!IsAtOrAboveSizeLimit(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directoryPath, $"{baseName}-{Guid.NewGuid():N}{_fileExtension}");
    }

    private bool IsAtOrAboveSizeLimit(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            return new FileInfo(filePath).Length >= GetMaxFileBytes();
        }
        catch (Exception ex)
        {
            SetWarning($"日志文件大小读取失败：{ex.Message}");
            return false;
        }
    }

    private void CleanupOldFiles(string directoryPath, DateTimeOffset now)
    {
        try
        {
            var retentionDays = GetRetentionDays();
            var cutoff = now.UtcDateTime.AddDays(-retentionDays);
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, $"{_filePrefix}-*{_fileExtension}", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(filePath);
                if (info.Exists && info.LastWriteTimeUtc < cutoff)
                {
                    info.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            SetWarning($"日志保留清理失败：{ex.Message}");
        }
    }

    private async Task CloseWriterAsync()
    {
        if (_writer is null)
        {
            return;
        }

        try
        {
            await _writer.FlushAsync().ConfigureAwait(false);
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetWarning($"日志关闭失败：{ex.Message}");
        }
        finally
        {
            _writer = null;
            _writerDate = null;
        }
    }

    private bool IsEnabled()
    {
        lock (_statusGate)
        {
            return !_stopping && _enabledSelector(_settings);
        }
    }

    private bool IsStopping()
    {
        lock (_statusGate)
        {
            return _stopping;
        }
    }

    private long GetMaxFileBytes()
    {
        lock (_statusGate)
        {
            return (long)_settings.MaxLogFileSizeMB * 1024L * 1024L;
        }
    }

    private int GetRetentionDays()
    {
        lock (_statusGate)
        {
            return _settings.MaxLogRetentionDays;
        }
    }

    private void SetWarning(string warning)
    {
        lock (_statusGate)
        {
            _lastWarning = warning;
        }
    }

    private static LogSettings Normalize(LogSettings settings)
    {
        return settings with
        {
            MaxLogFileSizeMB = Math.Clamp(settings.MaxLogFileSizeMB, 1, 1024),
            MaxLogRetentionDays = Math.Clamp(settings.MaxLogRetentionDays, 1, 366)
        };
    }
}
