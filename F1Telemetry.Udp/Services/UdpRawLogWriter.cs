using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using F1Telemetry.Core;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;

namespace F1Telemetry.Udp.Services;

/// <summary>
/// Writes optional raw UDP packet JSONL logs on a bounded background queue.
/// </summary>
public sealed class UdpRawLogWriter : IUdpRawLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentQueue<UdpDatagram> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly PacketHeaderParser _headerParser;
    private readonly Task _worker;
    private readonly object _statusGate = new();
    private bool _enabled;
    private bool _stopping;
    private int _queuedCount;
    private int _queueCapacity = 4096;
    private StreamWriter? _writer;
    private string _directoryPath = GetDefaultDirectoryPath();
    private string _currentFilePath = string.Empty;
    private string _lastError = string.Empty;
    private long _writtenPacketCount;
    private long _droppedPacketCount;

    /// <summary>
    /// Initializes a raw UDP log writer.
    /// </summary>
    public UdpRawLogWriter()
        : this(new PacketHeaderParser())
    {
    }

    internal UdpRawLogWriter(PacketHeaderParser headerParser)
    {
        _headerParser = headerParser ?? throw new ArgumentNullException(nameof(headerParser));
        _worker = Task.Run(ProcessQueueAsync);
    }

    /// <inheritdoc />
    public UdpRawLogStatus Status
    {
        get
        {
            lock (_statusGate)
            {
                return new UdpRawLogStatus
                {
                    Enabled = _enabled,
                    DirectoryPath = _directoryPath,
                    CurrentFilePath = _currentFilePath,
                    WrittenPacketCount = Interlocked.Read(ref _writtenPacketCount),
                    DroppedPacketCount = Interlocked.Read(ref _droppedPacketCount),
                    LastError = _lastError
                };
            }
        }
    }

    /// <inheritdoc />
    public void UpdateOptions(UdpRawLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_statusGate)
        {
            _enabled = options.Enabled;
            _directoryPath = ResolveDirectoryPath(options.DirectoryPath);
            _queueCapacity = Math.Max(0, options.QueueCapacity);
            if (_enabled)
            {
                _lastError = string.Empty;
            }
        }
    }

    /// <inheritdoc />
    public void TryEnqueue(UdpDatagram datagram)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        if (!_enabled)
        {
            return;
        }

        var capacity = Volatile.Read(ref _queueCapacity);
        if (capacity <= 0)
        {
            Interlocked.Increment(ref _droppedPacketCount);
            return;
        }

        var queuedCount = Interlocked.Increment(ref _queuedCount);
        if (queuedCount > capacity)
        {
            Interlocked.Decrement(ref _queuedCount);
            Interlocked.Increment(ref _droppedPacketCount);
            return;
        }

        _queue.Enqueue(datagram);
        _signal.Release();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_statusGate)
        {
            _enabled = false;
            _stopping = true;
        }

        _signal.Release();
        await _worker.ConfigureAwait(false);
        _signal.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            await _signal.WaitAsync().ConfigureAwait(false);

            while (_queue.TryDequeue(out var datagram))
            {
                Interlocked.Decrement(ref _queuedCount);
                await WriteDatagramAsync(datagram).ConfigureAwait(false);
            }

            if (_stopping && _queue.IsEmpty)
            {
                await CloseWriterAsync().ConfigureAwait(false);
                return;
            }
        }
    }

    private async Task WriteDatagramAsync(UdpDatagram datagram)
    {
        try
        {
            var writer = EnsureWriter();
            var record = BuildRecord(datagram);
            var line = JsonSerializer.Serialize(record, JsonOptions);
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            Interlocked.Increment(ref _writtenPacketCount);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _droppedPacketCount);
            lock (_statusGate)
            {
                _lastError = $"Raw Log 写入失败：{ex.Message}";
            }
        }
    }

    private StreamWriter EnsureWriter()
    {
        if (_writer is not null)
        {
            return _writer;
        }

        string directoryPath;
        lock (_statusGate)
        {
            directoryPath = _directoryPath;
        }

        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(
            directoryPath,
            $"f1telemetry-udp-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-session-unknown.jsonl");
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream);
        lock (_statusGate)
        {
            _currentFilePath = filePath;
        }

        return _writer;
    }

    private async Task CloseWriterAsync()
    {
        if (_writer is null)
        {
            return;
        }

        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
        _writer = null;
    }

    private UdpRawLogRecord BuildRecord(UdpDatagram datagram)
    {
        PacketHeader? header = null;
        if (_headerParser.TryParse(datagram.Payload, out var parsedHeader, out _))
        {
            header = parsedHeader;
        }

        return new UdpRawLogRecord
        {
            TimestampUtc = datagram.ReceivedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Source = datagram.RemoteEndPoint.ToString(),
            Length = datagram.Payload.Length,
            PacketId = header?.RawPacketId,
            SessionUid = header?.SessionUid,
            FrameIdentifier = header?.FrameIdentifier,
            PlayerCarIndex = header?.PlayerCarIndex,
            PacketFormat = header?.PacketFormat,
            GameYear = header?.GameYear,
            PacketVersion = header?.PacketVersion,
            PayloadBase64 = Convert.ToBase64String(datagram.Payload)
        };
    }

    private static string ResolveDirectoryPath(string directoryPath)
    {
        return string.IsNullOrWhiteSpace(directoryPath)
            ? GetDefaultDirectoryPath()
            : directoryPath.Trim();
    }

    private static string GetDefaultDirectoryPath()
    {
        return Path.Combine(AppPaths.GetAppDataDir(), ".logs", "udp");
    }

    private sealed record UdpRawLogRecord
    {
        public required string TimestampUtc { get; init; }

        public required string Source { get; init; }

        public int Length { get; init; }

        public byte? PacketId { get; init; }

        public ulong? SessionUid { get; init; }

        public uint? FrameIdentifier { get; init; }

        public byte? PlayerCarIndex { get; init; }

        public ushort? PacketFormat { get; init; }

        public byte? GameYear { get; init; }

        public byte? PacketVersion { get; init; }

        public required string PayloadBase64 { get; init; }
    }
}
