using System.Net;
using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Interfaces;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Services;
using F1Telemetry.Analytics.State;
using F1Telemetry.App.Charts;
using F1Telemetry.App.ViewModels;
using F1Telemetry.Core.Interfaces;
using F1Telemetry.Core.Models;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.TTS;
using F1Telemetry.TTS.Models;
using F1Telemetry.TTS.Services;
using F1Telemetry.Udp.Packets;
using Xunit;

namespace F1Telemetry.Tests;

#pragma warning disable CS0067

/// <summary>
/// Verifies dashboard chart lifecycle behavior around session transitions.
/// </summary>
public sealed class DashboardChartStateTests
{
    /// <summary>
    /// Verifies that a new UDP session clears all chart panels to their empty states.
    /// </summary>
    [Fact]
    public void SessionUidChange_ClearsAllChartPanels()
    {
        RunOnStaThread(() =>
        {
            var dispatcher = new FakePacketDispatcher();
            var viewModel = CreateDashboardViewModel(dispatcher);

            try
            {
                viewModel.SpeedChartPanel.UpdateFrom(CreateDataPanel("当前圈速度曲线", "等待本圈采样"));
                viewModel.InputsChartPanel.UpdateFrom(CreateDataPanel("当前圈油门 / 刹车曲线", "等待输入数据"));
                viewModel.FuelTrendChartPanel.UpdateFrom(CreateDataPanel("多圈燃油趋势", "完成至少一圈后显示"));
                viewModel.TyreWearTrendChartPanel.UpdateFrom(CreateDataPanel("多圈四轮磨损趋势", "等待轮胎磨损数据"));

                Assert.All(
                    new[]
                    {
                        viewModel.SpeedChartPanel,
                        viewModel.InputsChartPanel,
                        viewModel.FuelTrendChartPanel,
                        viewModel.TyreWearTrendChartPanel
                    },
                    panel => Assert.True(panel.HasData));

                dispatcher.RaiseSession(456UL);

                Assert.False(viewModel.SpeedChartPanel.HasData);
                Assert.Equal("等待本圈采样", viewModel.SpeedChartPanel.EmptyStateText);
                Assert.Empty(viewModel.SpeedChartPanel.Series);
                Assert.False(viewModel.InputsChartPanel.HasData);
                Assert.Equal("等待输入数据", viewModel.InputsChartPanel.EmptyStateText);
                Assert.Empty(viewModel.InputsChartPanel.Series);
                Assert.False(viewModel.FuelTrendChartPanel.HasData);
                Assert.Equal("完成至少一圈后显示", viewModel.FuelTrendChartPanel.EmptyStateText);
                Assert.Empty(viewModel.FuelTrendChartPanel.Series);
                Assert.False(viewModel.TyreWearTrendChartPanel.HasData);
                Assert.Equal("等待轮胎磨损数据", viewModel.TyreWearTrendChartPanel.EmptyStateText);
                Assert.Empty(viewModel.TyreWearTrendChartPanel.Series);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies tyre condition summaries switch from waiting text to live telemetry values.
    /// </summary>
    [Fact]
    public void RefreshCentralState_WithTyreCondition_ShowsTyreTemperatureAndPressureSummary()
    {
        RunOnStaThread(() =>
        {
            var dispatcher = new FakePacketDispatcher();
            var sessionStateStore = new SessionStateStore(new CarStateStore());
            var viewModel = CreateDashboardViewModel(dispatcher, sessionStateStore);

            try
            {
                Assert.Equal("等待数据", viewModel.OverviewTyreTemperatureText);
                Assert.Equal("等待数据", viewModel.OverviewTyrePressureText);

                var aggregator = new StateAggregator(sessionStateStore);
                aggregator.ApplyPacket(CreateParsedPacket(
                    new CarTelemetryPacket(
                        BuildTelemetryCars(),
                        MfdPanelIndex: 255,
                        MfdPanelIndexSecondaryPlayer: 255,
                        SuggestedGear: 0),
                    playerCarIndex: 0));

                InvokeRefreshCentralState(viewModel);

                Assert.Equal("表 90-105°C · 内 80-100°C", viewModel.OverviewTyreTemperatureText);
                Assert.Equal("21.1-21.4 psi", viewModel.OverviewTyrePressureText);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies a bulk backfill of official lap history is persisted as individual lap summaries.
    /// </summary>
    [Fact]
    public void RefreshCentralState_PersistsAllUnstoredLapSummaries()
    {
        RunOnStaThread(() =>
        {
            var storage = new FakeStoragePersistenceService();
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: new FakeLapAnalyzer(
                [
                    new LapSummary { LapNumber = 1, LapTimeInMs = 90_000, ClosedAt = DateTimeOffset.UtcNow },
                    new LapSummary { LapNumber = 2, LapTimeInMs = 89_500, ClosedAt = DateTimeOffset.UtcNow }
                ]),
                storagePersistenceService: storage);

            try
            {
                InvokeRefreshCentralState(viewModel);
                InvokeRefreshCentralState(viewModel);

                Assert.Equal(new[] { 1, 2 }, storage.EnqueuedLapSummaries.Select(lap => lap.LapNumber));
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    private static DashboardViewModel CreateDashboardViewModel(
        FakePacketDispatcher dispatcher,
        SessionStateStore? sessionStateStore = null,
        ILapAnalyzer? lapAnalyzer = null,
        FakeStoragePersistenceService? storagePersistenceService = null)
    {
        var ttsQueue = new TtsQueue(new FakeTtsService(), new TtsOptions());
        return new DashboardViewModel(
            new FakeUdpListener(),
            dispatcher,
            sessionStateStore ?? new SessionStateStore(new CarStateStore()),
            lapAnalyzer ?? new LapAnalyzer(),
            new EventDetectionService(),
            new FakeAiAnalysisService(),
            new FakeAppSettingsStore(),
            new FakeUdpRawLogWriter(),
            new TtsMessageFactory(),
            ttsQueue,
            storagePersistenceService ?? new FakeStoragePersistenceService(),
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")));
    }

    private static void InvokeRefreshCentralState(DashboardViewModel viewModel)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "RefreshCentralState",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, null);
    }

    private static ParsedPacket CreateParsedPacket(IUdpPacket packet, byte playerCarIndex)
    {
        var header = new PacketHeader(
            PacketFormat: 2025,
            GameYear: 25,
            GameMajorVersion: 1,
            GameMinorVersion: 0,
            PacketVersion: 1,
            RawPacketId: (byte)PacketId.CarTelemetry,
            SessionUid: 123UL,
            SessionTime: 0,
            FrameIdentifier: 1,
            OverallFrameIdentifier: 1,
            PlayerCarIndex: playerCarIndex,
            SecondaryPlayerCarIndex: 255);
        var datagram = new UdpDatagram(Array.Empty<byte>(), new IPEndPoint(IPAddress.Loopback, 20777), DateTimeOffset.UtcNow);
        return new ParsedPacket(PacketId.CarTelemetry, header, packet, datagram);
    }

    private static CarTelemetryData[] BuildTelemetryCars()
    {
        var cars = new CarTelemetryData[22];
        for (var index = 0; index < cars.Length; index++)
        {
            cars[index] = new CarTelemetryData(
                Speed: 200,
                Throttle: 0.5f,
                Steer: 0f,
                Brake: 0.2f,
                Clutch: 0,
                Gear: 4,
                EngineRpm: 11000,
                Drs: false,
                RevLightsPercent: 0,
                RevLightsBitValue: 0,
                BrakesTemperature: new WheelSet<ushort>(500, 500, 500, 500),
                TyresSurfaceTemperature: new WheelSet<byte>(90, 95, 100, 105),
                TyresInnerTemperature: new WheelSet<byte>(80, 85, 90, 100),
                EngineTemperature: 100,
                TyresPressure: new WheelSet<float>(21.1f, 21.2f, 21.3f, 21.4f),
                SurfaceType: new WheelSet<byte>(0, 0, 0, 0));
        }

        return cars;
    }

    private static ChartPanelViewModel CreateDataPanel(string title, string emptyStateText)
    {
        return new ChartPanelViewModel(
            title: title,
            xAxisLabel: "x",
            yAxisLabel: "y",
            emptyMessage: emptyStateText,
            isEmpty: false,
            series:
            [
                new ChartSeriesModel
                {
                    Name = "数据",
                    Points =
                    [
                        new ChartPointModel { X = 1d, Y = 2d }
                    ]
                }
            ]);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }

    private sealed class FakeUdpListener : IUdpListener
    {
        public event EventHandler<UdpDatagram>? DatagramReceived;

        public event EventHandler<Exception>? ReceiveFaulted;

        public bool IsListening { get; private set; }

        public int? ListeningPort { get; private set; }

        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            IsListening = true;
            ListeningPort = port;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsListening = false;
            ListeningPort = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsListening = false;
            ListeningPort = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakePacketDispatcher : IPacketDispatcher<PacketId, PacketHeader>
    {
        public event EventHandler<PacketDispatchResult<PacketId, PacketHeader>>? PacketDispatched;

        public bool TryDispatch(UdpDatagram datagram, out string? error)
        {
            error = null;
            return true;
        }

        public void RaiseSession(ulong sessionUid)
        {
            var header = new PacketHeader(
                PacketFormat: 2025,
                GameYear: 25,
                GameMajorVersion: 1,
                GameMinorVersion: 0,
                PacketVersion: 1,
                RawPacketId: (byte)PacketId.Session,
                SessionUid: sessionUid,
                SessionTime: 0,
                FrameIdentifier: 1,
                OverallFrameIdentifier: 1,
                PlayerCarIndex: 0,
                SecondaryPlayerCarIndex: 255);
            var datagram = new UdpDatagram(Array.Empty<byte>(), new IPEndPoint(IPAddress.Loopback, 20777), DateTimeOffset.UtcNow);
            PacketDispatched?.Invoke(this, new PacketDispatchResult<PacketId, PacketHeader>(PacketId.Session, header, datagram));
        }
    }

    private sealed class FakeAiAnalysisService : IAIAnalysisService
    {
        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AIAnalysisResult());
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppSettingsDocument());
        }

        public Task SaveAiSettingsAsync(AISettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveTtsSettingsAsync(TtsOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpSettingsAsync(UdpSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUdpRawLogWriter : IUdpRawLogWriter
    {
        public UdpRawLogStatus Status { get; private set; } = new();

        public void UpdateOptions(UdpRawLogOptions options)
        {
            Status = Status with
            {
                Enabled = options.Enabled,
                DirectoryPath = options.DirectoryPath
            };
        }

        public void TryEnqueue(UdpDatagram datagram)
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeStoragePersistenceService : IStoragePersistenceService
    {
        public event EventHandler<string>? LogEmitted;

        public List<LapSummary> EnqueuedLapSummaries { get; } = [];

        public void ObserveParsedPacket(ParsedPacket parsedPacket)
        {
        }

        public void EnqueueLapSummary(LapSummary lapSummary)
        {
            EnqueuedLapSummaries.Add(lapSummary);
        }

        public void EnqueueRaceEvent(RaceEvent raceEvent)
        {
        }

        public void EnqueueAiReport(int lapNumber, AIAnalysisResult analysisResult)
        {
        }

        public Task CompleteActiveSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeLapAnalyzer(IReadOnlyList<LapSummary> laps) : ILapAnalyzer
    {
        public void Observe(ParsedPacket parsedPacket, SessionState sessionState)
        {
        }

        public void ResetForSession(ulong sessionUid)
        {
        }

        public IReadOnlyList<LapSummary> CaptureAllLaps()
        {
            return laps;
        }

        public IReadOnlyList<LapSample> CaptureCurrentLapSamples()
        {
            return Array.Empty<LapSample>();
        }

        public IReadOnlyList<LapSummary> CaptureRecentLaps(int maxCount)
        {
            return laps.Take(maxCount).Reverse().ToArray();
        }

        public LapSummary? CaptureBestLap()
        {
            return laps.OrderBy(lap => lap.LapTimeInMs).FirstOrDefault();
        }

        public LapSummary? CaptureLastLap()
        {
            return laps.LastOrDefault();
        }
    }

    private sealed class FakeTtsService : ITtsService, IDisposable
    {
        public void Configure(string? voiceName, int volume, int rate)
        {
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}

#pragma warning restore CS0067
