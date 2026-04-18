using System.Globalization;
using System.Threading.Channels;
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
    private readonly ISessionRepository _sessionRepository;
    private readonly ILapRepository _lapRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IAIReportRepository _aiReportRepository;
    private readonly Func<CancellationToken, Task>? _initializeAsync;
    private readonly IDatabaseService? _ownedDatabaseService;
    private readonly Channel<StorageCommand> _commands = Channel.CreateUnbounded<StorageCommand>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
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
        IDatabaseService? ownedDatabaseService = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _lapRepository = lapRepository ?? throw new ArgumentNullException(nameof(lapRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _aiReportRepository = aiReportRepository ?? throw new ArgumentNullException(nameof(aiReportRepository));
        _initializeAsync = initializeAsync;
        _ownedDatabaseService = ownedDatabaseService;
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
        EnqueueCommand(new CompleteActiveSessionCommand(DateTimeOffset.UtcNow, completion));

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
        _commands.Writer.TryComplete();

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

            _workerCts.Cancel();
            _workerCts.Dispose();
        }
    }

    private void EnqueueCommand(StorageCommand command)
    {
        if (_disposed)
        {
            return;
        }

        if (!_commands.Writer.TryWrite(command))
        {
            EmitLog("SQLite 队列已关闭，已跳过一次持久化请求。");
        }
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

        await foreach (var command in _commands.Reader.ReadAllAsync(_workerCts.Token))
        {
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

    private abstract record StorageCommand;

    private sealed record ObserveSessionPacketCommand(
        string SessionUid,
        int? TrackId,
        int? SessionType,
        DateTimeOffset ObservedAt) : StorageCommand;

    private sealed record PersistLapCommand(LapSummary LapSummary) : StorageCommand;

    private sealed record PersistEventCommand(RaceEvent RaceEvent) : StorageCommand;

    private sealed record PersistAiReportCommand(
        int LapNumber,
        AIAnalysisResult AnalysisResult,
        DateTimeOffset CreatedAt) : StorageCommand;

    private sealed record CompleteActiveSessionCommand(
        DateTimeOffset EndedAt,
        TaskCompletionSource Completion) : StorageCommand;
}
