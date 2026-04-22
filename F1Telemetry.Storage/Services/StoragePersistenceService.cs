using System.Globalization;
using F1Telemetry.AI.Models;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.Storage.Services;

/// <summary>
/// Serializes SQLite writes onto a single background worker so real-time telemetry remains responsive.
/// </summary>
public sealed class StoragePersistenceService : IStoragePersistenceService
{
    private const int DefaultMaxBufferedCommands = 1024;
    private const int DefaultMaxCriticalCommands = 32;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILapRepository _lapRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IAIReportRepository _aiReportRepository;
    private readonly Func<CancellationToken, Task>? _initializeAsync;
    private readonly IDatabaseService? _ownedDatabaseService;
    private readonly object _queueLock = new();
    private readonly Queue<StorageCommand> _criticalCommands = new();
    private readonly Queue<StorageCommand> _bufferedCommands = new();
    private readonly SemaphoreSlim _commandSignal = new(0);
    private readonly int _maxBufferedCommands;
    private readonly int _maxCriticalCommands;
    private readonly CancellationTokenSource _workerCts = new();
    private readonly Task _workerTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new persistence coordinator.
    /// </summary>
    public StoragePersistenceService(
        ISessionRepository sessionRepository,
        ILapRepository lapRepository,
        IEventRepository eventRepository,
        IAIReportRepository aiReportRepository,
        Func<CancellationToken, Task>? initializeAsync = null,
        IDatabaseService? ownedDatabaseService = null,
        int maxBufferedCommands = DefaultMaxBufferedCommands,
        int maxCriticalCommands = DefaultMaxCriticalCommands)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferedCommands);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCriticalCommands);

        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _aiReportRepository = aiReportRepository ?? throw new ArgumentNullException(nameof(aiReportRepository));
        _initializeAsync = initializeAsync;
        _ownedDatabaseService = ownedDatabaseService;
        _maxBufferedCommands = maxBufferedCommands;
        _maxCriticalCommands = maxCriticalCommands;
        _workerTask = Task.Run(ProcessCommandsAsync);
    }

    /// <inheritdoc />
    public event EventHandler<string>? LogEmitted;

    /// <inheritdoc />
    public void ObserveParsedPacket(ParsedPacket parsedPacket)
    {
        ArgumentNullException.ThrowIfNull(parsedPacket);

        if (parsedPacket.PacketId != PacketId.Session || parsedPacket.Packet is not SessionPacket sessionPacket)
        {
            return;
        }

        EnqueueCommand(
            new ObserveSessionPacketCommand(
                parsedPacket.Header.SessionUid.ToString(CultureInfo.InvariantCulture),
                sessionPacket.TrackId,
                sessionPacket.SessionType,
                parsedPacket.Datagram.ReceivedAt));
    }

    /// <inheritdoc />
    public void EnqueueLapSummary(LapSummary lapSummary)
    {
        ArgumentNullException.ThrowIfNull(lapSummary);
        EnqueueCommand(new PersistLapCommand(lapSummary));
    }

    /// <inheritdoc />
    public void EnqueueRaceEvent(RaceEvent raceEvent)
    {
        ArgumentNullException.ThrowIfNull(raceEvent);
        EnqueueCommand(new PersistEventCommand(raceEvent));
    }

    /// <inheritdoc />
    public void EnqueueAiReport(int lapNumber, AIAnalysisResult analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);
        EnqueueCommand(new PersistAiReportCommand(lapNumber, analysisResult, DateTimeOffset.UtcNow));
    }

    /// <inheritdoc />
    public async Task CompleteActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!EnqueueCommand(
                new CompleteActiveSessionCommand(DateTimeOffset.UtcNow, completion),
                "SQLite 队列已满，停止会话请求可能延迟。"))
        {
            completion.TrySetResult();
        }

        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        await completion.Task;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await CompleteActiveSessionAsync();
        }
        catch
        {
        }

        _disposed = true;
        _commandSignal.Release();

        try
        {
            await _workerTask;
        }
        catch
        {
        }
        finally
        {
            if (_ownedDatabaseService is not null)
            {
                await _ownedDatabaseService.DisposeAsync();
            }

            _commandSignal.Dispose();
            _workerCts.Cancel();
            _workerCts.Dispose();
        }
    }

    private void EnqueueCommand(StorageCommand command)
    {
        _ = EnqueueCommand(command, "SQLite 持久化队列已满，已跳过一次持久化请求。");
    }

    private bool EnqueueCommand(StorageCommand command, string dropMessage)
    {
        if (_disposed)
        {
            return false;
        }

        string? logMessage = null;
        var accepted = false;

        lock (_queueLock)
        {
            if (_disposed)
            {
                return false;
            }

            if (command.IsCritical)
            {
                if (_criticalCommands.Count >= _maxCriticalCommands)
                {
                    logMessage = "SQLite 关键队列已满，已跳过一次会话生命周期请求。";
                }
                else
                {
                    _criticalCommands.Enqueue(command);
                    accepted = true;
                }
            }
            else if (_bufferedCommands.Count >= _maxBufferedCommands)
            {
                logMessage = dropMessage;
            }
            else
            {
                _bufferedCommands.Enqueue(command);
                accepted = true;
            }
        }

        if (!accepted)
        {
            if (command is CompleteActiveSessionCommand completeSession)
            {
                completeSession.Completion.TrySetResult();
            }

            if (!string.IsNullOrWhiteSpace(logMessage))
            {
                EmitLog(logMessage);
            }

            return false;
        }

        _commandSignal.Release();
        return true;
    }

    private async Task ProcessCommandsAsync()
    {
        string? activeSessionId = null;
        string? activeSessionUid = null;

        if (_initializeAsync is not null)
        {
            try
            {
                await _initializeAsync(_workerCts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                EmitLog($"SQLite 初始化失败：{ex.Message}");
            }
        }

        while (true)
        {
            try
            {
                await _commandSignal.WaitAsync(_workerCts.Token);
            }
            catch (OperationCanceledException) when (_workerCts.IsCancellationRequested)
            {
                break;
            }

            StorageCommand? command;
            lock (_queueLock)
            {
                command = DequeueNextCommandUnsafe();
                if (command is null)
                {
                    if (_disposed)
                    {
                        break;
                    }

                    continue;
                }
            }

            try
            {
                switch (command)
                {
                    case ObserveSessionPacketCommand observeSession:
                        (activeSessionId, activeSessionUid) = await HandleObserveSessionAsync(
                            observeSession,
                            activeSessionId,
                            activeSessionUid);
                        break;

                    case PersistLapCommand persistLap:
                        if (activeSessionId is null)
                        {
                            EmitLog("未发现活动会话，已跳过圈摘要持久化。");
                            break;
                        }

                        await _lapRepository.AddAsync(activeSessionId, persistLap.LapSummary, _workerCts.Token);
                        break;

                    case PersistEventCommand persistEvent:
                        if (activeSessionId is null)
                        {
                            EmitLog("未发现活动会话，已跳过事件持久化。");
                            break;
                        }

                        await _eventRepository.AddAsync(activeSessionId, persistEvent.RaceEvent, _workerCts.Token);
                        break;

                    case PersistAiReportCommand persistAiReport:
                        if (activeSessionId is null)
                        {
                            EmitLog("未发现活动会话，已跳过 AI 分析持久化。");
                            break;
                        }

                        await _aiReportRepository.AddAsync(
                            activeSessionId,
                            persistAiReport.LapNumber,
                            persistAiReport.AnalysisResult,
                            persistAiReport.CreatedAt,
                            _workerCts.Token);
                        break;

                    case CompleteActiveSessionCommand completeSession:
                        await CompleteActiveSessionInternalAsync(activeSessionId, completeSession.EndedAt);
                        activeSessionId = null;
                        activeSessionUid = null;
                        completeSession.Completion.TrySetResult();
                        break;
                }
            }
            catch (OperationCanceledException) when (_workerCts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                EmitLog($"SQLite 持久化失败：{ex.Message}");
                if (command is CompleteActiveSessionCommand completeSession)
                {
                    completeSession.Completion.TrySetResult();
                }
            }
        }
    }

    private StorageCommand? DequeueNextCommandUnsafe()
    {
        if (_criticalCommands.Count > 0)
        {
            return _criticalCommands.Dequeue();
        }

        return _bufferedCommands.Count > 0 ? _bufferedCommands.Dequeue() : null;
    }

    private async Task<(string? ActiveSessionId, string? ActiveSessionUid)> HandleObserveSessionAsync(
        ObserveSessionPacketCommand command,
        string? activeSessionId,
        string? activeSessionUid)
    {
        if (string.Equals(activeSessionUid, command.SessionUid, StringComparison.Ordinal))
        {
            return (activeSessionId, activeSessionUid);
        }

        if (activeSessionId is not null)
        {
            await CompleteActiveSessionInternalAsync(activeSessionId, command.ObservedAt);
            activeSessionId = null;
            activeSessionUid = null;
        }

        var session = new StoredSession
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionUid = command.SessionUid,
            TrackId = command.TrackId,
            SessionType = command.SessionType,
            StartedAt = command.ObservedAt
        };

        await _sessionRepository.CreateAsync(session, _workerCts.Token);
        EmitLog($"已创建 SQLite 会话记录：{session.SessionUid}");
        return (session.Id, session.SessionUid);
    }

    private async Task CompleteActiveSessionInternalAsync(string? activeSessionId, DateTimeOffset endedAt)
    {
        if (string.IsNullOrWhiteSpace(activeSessionId))
        {
            return;
        }

        await _sessionRepository.EndAsync(activeSessionId, endedAt, _workerCts.Token);
        EmitLog("已结束当前 SQLite 会话记录。");
    }

    private void EmitLog(string message)
    {
        LogEmitted?.Invoke(this, message);
    }

    private abstract record StorageCommand(bool IsCritical);

    private sealed record ObserveSessionPacketCommand(
        string SessionUid,
        int? TrackId,
        int? SessionType,
        DateTimeOffset ObservedAt) : StorageCommand(true);

    private sealed record PersistLapCommand(LapSummary LapSummary) : StorageCommand(false);

    private sealed record PersistEventCommand(RaceEvent RaceEvent) : StorageCommand(false);

    private sealed record PersistAiReportCommand(
        int LapNumber,
        AIAnalysisResult AnalysisResult,
        DateTimeOffset CreatedAt) : StorageCommand(false);

    private sealed record CompleteActiveSessionCommand(
        DateTimeOffset EndedAt,
        TaskCompletionSource Completion) : StorageCommand(true);
}
