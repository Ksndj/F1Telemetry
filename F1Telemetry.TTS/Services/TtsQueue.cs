using System.Collections.ObjectModel;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.TTS.Models;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Queues TTS messages and applies deduplication, cooldown, priority, and queue overflow rules on a single background worker.
/// </summary>
public sealed class TtsQueue : IDisposable
{
    private const int MaxQueuedMessages = 50;

    private readonly ITtsService _ttsService;
    private readonly object _sync = new();
    private readonly Queue<TtsMessage> _highPriorityMessages = new();
    private readonly Queue<TtsMessage> _normalPriorityMessages = new();
    private readonly Queue<TtsMessage> _lowPriorityMessages = new();
    private readonly HashSet<string> _activeDedupKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastSuccessfulPlaybackAt = new(StringComparer.Ordinal);
    private readonly Queue<TtsPlaybackRecord> _pendingRecords = new();
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly Task _workerTask;
    private TtsOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new queue instance.
    /// </summary>
    /// <param name="ttsService">The low-level speech service used by the single consumer worker.</param>
    /// <param name="options">The initial TTS playback options.</param>
    public TtsQueue(ITtsService ttsService, TtsOptions options)
    {
        _ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Updates the TTS settings used for future playback.
    /// </summary>
    /// <param name="options">The new TTS options snapshot.</param>
    public void UpdateOptions(TtsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_sync)
        {
            ThrowIfDisposed();
            _options = options;
        }
    }

    /// <summary>
    /// Attempts to enqueue a TTS message.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <returns><see langword="true"/> when the message was accepted; otherwise <see langword="false"/>.</returns>
    public bool TryEnqueue(TtsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return false;
        }

        var normalizedMessage = message with
        {
            Text = message.Text.Trim(),
            Type = string.IsNullOrWhiteSpace(message.Type) ? "message" : message.Type.Trim(),
            DedupKey = message.DedupKey?.Trim() ?? string.Empty,
            Source = string.IsNullOrWhiteSpace(message.Source) ? "TTS" : message.Source.Trim(),
            Cooldown = NormalizeCooldown(message.Cooldown)
        };

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!_options.TtsEnabled)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(normalizedMessage.DedupKey) &&
                _activeDedupKeys.Contains(normalizedMessage.DedupKey))
            {
                AddRecordUnsafe(
                    CreateRecord(
                        normalizedMessage,
                        TtsPlaybackOutcome.Deduplicated,
                        $"已去重，忽略重复播报：{normalizedMessage.Text}"));
                return false;
            }

            if (!string.IsNullOrWhiteSpace(normalizedMessage.DedupKey) &&
                _lastSuccessfulPlaybackAt.TryGetValue(normalizedMessage.DedupKey, out var lastSuccessfulPlaybackAt) &&
                DateTimeOffset.UtcNow - lastSuccessfulPlaybackAt < normalizedMessage.Cooldown)
            {
                AddRecordUnsafe(
                    CreateRecord(
                        normalizedMessage,
                        TtsPlaybackOutcome.CooledDown,
                        $"冷却中，暂不重复播报：{normalizedMessage.Text}"));
                return false;
            }

            if (GetQueuedCountUnsafe() >= MaxQueuedMessages)
            {
                if (normalizedMessage.Priority == TtsPriority.Low)
                {
                    AddRecordUnsafe(
                        CreateRecord(
                            normalizedMessage,
                            TtsPlaybackOutcome.Dropped,
                            $"队列已满，已丢弃低优先级播报：{normalizedMessage.Text}"));
                    return false;
                }

                if (TryDequeueOldestLowPriorityUnsafe(out var droppedLowPriorityMessage))
                {
                    ReleaseDedupKeyUnsafe(droppedLowPriorityMessage);
                    AddRecordUnsafe(
                        CreateRecord(
                            droppedLowPriorityMessage,
                            TtsPlaybackOutcome.Dropped,
                            $"队列已满，已让出低优先级播报：{droppedLowPriorityMessage.Text}"));
                }
                else
                {
                    AddRecordUnsafe(
                        CreateRecord(
                            normalizedMessage,
                            TtsPlaybackOutcome.Dropped,
                            $"队列已满，无法插入高优先级播报：{normalizedMessage.Text}"));
                    return false;
                }
            }

            GetQueueUnsafe(normalizedMessage.Priority).Enqueue(normalizedMessage);

            if (!string.IsNullOrWhiteSpace(normalizedMessage.DedupKey))
            {
                _activeDedupKeys.Add(normalizedMessage.DedupKey);
            }
        }

        _queueSignal.Release();
        return true;
    }

    /// <summary>
    /// Returns and clears new playback records since the previous drain.
    /// </summary>
    /// <returns>The pending playback records in insertion order.</returns>
    public IReadOnlyList<TtsPlaybackRecord> DrainPendingRecords()
    {
        lock (_sync)
        {
            var records = new List<TtsPlaybackRecord>(_pendingRecords.Count);
            while (_pendingRecords.Count > 0)
            {
                records.Add(_pendingRecords.Dequeue());
            }

            return new ReadOnlyCollection<TtsPlaybackRecord>(records);
        }
    }

    /// <summary>
    /// Disposes the queue and stops background playback.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _lifecycleCts.Cancel();
        _queueSignal.Release();

        try
        {
            _workerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _queueSignal.Dispose();
            _lifecycleCts.Dispose();

            if (_ttsService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (!_lifecycleCts.IsCancellationRequested)
        {
            try
            {
                await _queueSignal.WaitAsync(_lifecycleCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            TtsMessage? message;
            TtsOptions options;

            lock (_sync)
            {
                message = DequeueNextUnsafe();
                options = _options;
            }

            if (message is null)
            {
                continue;
            }

            try
            {
                if (!options.TtsEnabled)
                {
                    lock (_sync)
                    {
                        AddRecordUnsafe(
                            CreateRecord(
                                message,
                                TtsPlaybackOutcome.Dropped,
                                $"TTS 已禁用，跳过播报：{message.Text}"));
                    }

                    continue;
                }

                lock (_sync)
                {
                    AddRecordUnsafe(
                        CreateRecord(
                            message,
                            TtsPlaybackOutcome.PlaybackStarted,
                            $"开始播报：{message.Text}"));
                }

                _ttsService.Configure(options.VoiceName, options.Volume, options.Rate);
                await _ttsService.SpeakAsync(message.Text, _lifecycleCts.Token);

                lock (_sync)
                {
                    if (!string.IsNullOrWhiteSpace(message.DedupKey))
                    {
                        _lastSuccessfulPlaybackAt[message.DedupKey] = DateTimeOffset.UtcNow;
                    }

                    AddRecordUnsafe(
                        CreateRecord(
                            message,
                            TtsPlaybackOutcome.PlaybackCompleted,
                            $"播报完成：{message.Text}"));
                }
            }
            catch (OperationCanceledException) when (_lifecycleCts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    AddRecordUnsafe(
                        CreateRecord(
                            message,
                            TtsPlaybackOutcome.Failed,
                            $"播报失败：{ex.Message}",
                            "System"));
                }
            }
            finally
            {
                lock (_sync)
                {
                    ReleaseDedupKeyUnsafe(message);
                }
            }
        }
    }

    private static TimeSpan NormalizeCooldown(TimeSpan cooldown)
    {
        return cooldown < TimeSpan.Zero ? TimeSpan.Zero : cooldown;
    }

    private int GetQueuedCountUnsafe()
    {
        return _highPriorityMessages.Count + _normalPriorityMessages.Count + _lowPriorityMessages.Count;
    }

    private bool TryDequeueOldestLowPriorityUnsafe(out TtsMessage droppedMessage)
    {
        if (_lowPriorityMessages.Count > 0)
        {
            droppedMessage = _lowPriorityMessages.Dequeue();
            return true;
        }

        droppedMessage = default!;
        return false;
    }

    private void ReleaseDedupKeyUnsafe(TtsMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.DedupKey))
        {
            _activeDedupKeys.Remove(message.DedupKey);
        }
    }

    private void AddRecordUnsafe(TtsPlaybackRecord record)
    {
        _pendingRecords.Enqueue(record);
    }

    private TtsPlaybackRecord CreateRecord(
        TtsMessage message,
        TtsPlaybackOutcome outcome,
        string recordMessage,
        string? sourceOverride = null)
    {
        return new TtsPlaybackRecord
        {
            Source = string.IsNullOrWhiteSpace(sourceOverride) ? message.Source : sourceOverride,
            DedupKey = message.DedupKey,
            Message = recordMessage,
            Outcome = outcome,
            Timestamp = DateTimeOffset.Now
        };
    }

    private TtsMessage? DequeueNextUnsafe()
    {
        if (_highPriorityMessages.Count > 0)
        {
            return _highPriorityMessages.Dequeue();
        }

        if (_normalPriorityMessages.Count > 0)
        {
            return _normalPriorityMessages.Dequeue();
        }

        return _lowPriorityMessages.Count > 0 ? _lowPriorityMessages.Dequeue() : null;
    }

    private Queue<TtsMessage> GetQueueUnsafe(TtsPriority priority)
    {
        return priority switch
        {
            TtsPriority.High => _highPriorityMessages,
            TtsPriority.Normal => _normalPriorityMessages,
            _ => _lowPriorityMessages
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
