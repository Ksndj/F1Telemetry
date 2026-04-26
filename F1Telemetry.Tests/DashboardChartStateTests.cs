using System.Net;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;
using F1Telemetry.AI.Services;
using F1Telemetry.Analytics.Events;
using F1Telemetry.Analytics.Laps;
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
                Assert.False(viewModel.InputsChartPanel.HasData);
                Assert.Equal("等待输入数据", viewModel.InputsChartPanel.EmptyStateText);
                Assert.False(viewModel.FuelTrendChartPanel.HasData);
                Assert.Equal("完成至少一圈后显示", viewModel.FuelTrendChartPanel.EmptyStateText);
                Assert.False(viewModel.TyreWearTrendChartPanel.HasData);
                Assert.Equal("等待轮胎磨损数据", viewModel.TyreWearTrendChartPanel.EmptyStateText);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    private static DashboardViewModel CreateDashboardViewModel(FakePacketDispatcher dispatcher)
    {
        var ttsQueue = new TtsQueue(new FakeTtsService(), new TtsOptions());
        return new DashboardViewModel(
            new FakeUdpListener(),
            dispatcher,
            new SessionStateStore(new CarStateStore()),
            new LapAnalyzer(),
            new EventDetectionService(),
            new FakeAiAnalysisService(),
            new FakeAppSettingsStore(),
            new FakeUdpRawLogWriter(),
            new TtsMessageFactory(),
            ttsQueue,
            new FakeStoragePersistenceService(),
            Dispatcher.CurrentDispatcher,
            new WindowsVoiceCatalog(() => new WindowsVoiceCatalogResult(Array.Empty<string>(), string.Empty, "No voices.")));
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

        public void ObserveParsedPacket(ParsedPacket parsedPacket)
        {
        }

        public void EnqueueLapSummary(LapSummary lapSummary)
        {
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
