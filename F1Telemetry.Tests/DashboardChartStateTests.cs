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
    /// Verifies a new UDP session clears stale post-race AI report details.
    /// </summary>
    [Fact]
    public void SessionUidChange_ClearsPostRaceAiReportDetails()
    {
        RunOnStaThread(() =>
        {
            var dispatcher = new FakePacketDispatcher();
            var viewModel = CreateDashboardViewModel(dispatcher);

            try
            {
                InvokeUpdatePostRaceAiReportDetails(
                    viewModel,
                    new AIAnalysisResult
                    {
                        IsSuccess = true,
                        Summary = "上一场比赛结论",
                        KeyProblems = ["上一场主要问题"],
                        StrategyReview = "上一场策略回顾",
                        TyreReview = "上一场轮胎表现",
                        ErsFuelReview = "上一场 ERS / 燃油",
                        OpponentReview = "上一场对手攻防",
                        Improvements = ["上一场改进建议"]
                    },
                    new LapSummary { LapNumber = 58 });

                Assert.True(viewModel.PostRaceAiHasReport);
                Assert.Equal("上一场比赛结论", viewModel.PostRaceAiReportSummaryText);

                dispatcher.RaiseSession(456UL);

                Assert.False(viewModel.PostRaceAiHasReport);
                Assert.Equal("最近分析：暂无", viewModel.PostRaceAiLastAnalysisText);
                Assert.Equal("暂无 AI 分析报告", viewModel.PostRaceAiReportSummaryText);
                Assert.Equal("等待完赛数据", viewModel.PostRaceAiKeyProblemsText);
                Assert.Equal("等待完赛数据", viewModel.PostRaceAiStrategyReviewText);
                Assert.Equal("等待完赛数据", viewModel.PostRaceAiTyreReviewText);
                Assert.Equal("等待完赛数据", viewModel.PostRaceAiErsFuelReviewText);
                Assert.Equal("等待完赛数据", viewModel.PostRaceAiOpponentReviewText);
                Assert.Equal("等待完赛数据", viewModel.PostRaceAiImprovementsText);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies failed post-race AI attempts clear stale report details.
    /// </summary>
    [Fact]
    public void UpdatePostRaceAiReportDetails_WithFailure_ClearsReportDetails()
    {
        RunOnStaThread(() =>
        {
            var viewModel = CreateDashboardViewModel(new FakePacketDispatcher());

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);

                InvokeUpdatePostRaceAiReportDetails(
                    viewModel,
                    new AIAnalysisResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "网络或 API 返回异常"
                    },
                    new LapSummary { LapNumber = 12 });

                AssertPostRaceAiReportCleared(viewModel, "网络或 API 返回异常");
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies status refreshes do not hide the latest generated or failed AI report state.
    /// </summary>
    [Fact]
    public void RefreshPostRaceAiStatus_PreservesGeneratedAndFailedReportState()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            aiService.Results.Enqueue(new AIAnalysisResult
            {
                IsSuccess = true,
                Summary = "稳定完赛",
                KeyProblems = ["进站偏晚"]
            });
            aiService.Results.Enqueue(new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = "网络错误"
            });
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";
            var sessionState = CreateCompletedRaceState();

            try
            {
                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: true);

                InvokeRefreshPostRaceAiStatus(viewModel, sessionState);

                Assert.Contains("已生成", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
                Assert.Contains("稳定完赛", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: true);

                InvokeRefreshPostRaceAiStatus(viewModel, sessionState);

                Assert.False(viewModel.PostRaceAiHasReport);
                Assert.Contains("生成失败", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
                Assert.Contains("网络错误", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
                Assert.Contains("网络错误", viewModel.PostRaceAiFailureReason, StringComparison.Ordinal);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies automatic generation is disabled when the current session is not ready for post-race summary.
    /// </summary>
    [Fact]
    public void PostRaceAiSummaryCommand_WithAutoDataNotReady_IsDisabled()
    {
        RunOnStaThread(() =>
        {
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer());
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";

            try
            {
                InvokeRefreshPostRaceAiStatus(viewModel, new SessionState());

                Assert.False(viewModel.CanGeneratePostRaceAiSummary);
                Assert.False(viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null));
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies manual post-race summary remains available when auto completion is still waiting for final classification.
    /// </summary>
    [Fact]
    public void PostRaceAiSummaryCommand_WithStagedRaceLap_AllowsManualGeneration()
    {
        RunOnStaThread(() =>
        {
            var sessionStateStore = new SessionStateStore(new CarStateStore());
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                sessionStateStore,
                lapAnalyzer: CreateSingleLapAnalyzer());
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";
            viewModel.AiBaseUrl = "https://api.example.com";
            viewModel.AiModel = "test-model";

            try
            {
                ApplyStagedRaceState(sessionStateStore);

                InvokeRefreshPostRaceAiStatus(viewModel, sessionStateStore.CaptureState());

                Assert.Contains("等待 FinalClassification", viewModel.PostRaceAiCompletionText, StringComparison.Ordinal);
                Assert.True(viewModel.CanGeneratePostRaceAiSummary);
                Assert.True(viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null));
                Assert.True(viewModel.RegeneratePostRaceAiSummaryCommand.CanExecute(null));
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies disabled AI prevents post-race summary commands from appearing actionable.
    /// </summary>
    [Fact]
    public void PostRaceAiSummaryCommand_WithAiDisabled_IsDisabledAndShowsReason()
    {
        RunOnStaThread(() =>
        {
            var sessionStateStore = new SessionStateStore(new CarStateStore());
            var aiService = new FakeAiAnalysisService();
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                sessionStateStore,
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = false;
            viewModel.AiApiKey = "test-key";
            viewModel.AiBaseUrl = "https://api.example.com";
            viewModel.AiModel = "test-model";

            try
            {
                ApplyStagedRaceState(sessionStateStore);

                InvokeRefreshPostRaceAiStatus(viewModel, sessionStateStore.CaptureState());

                Assert.False(viewModel.CanGeneratePostRaceAiSummary);
                Assert.False(viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null));
                Assert.False(viewModel.RegeneratePostRaceAiSummaryCommand.CanExecute(null));
                Assert.Contains("AI 未启用", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
                Assert.Contains("AI 未启用", viewModel.PostRaceAiDataStatusText, StringComparison.Ordinal);
                Assert.Equal("请先在 AI/TTS 设置中启用 AI", viewModel.PostRaceAiSummaryCommandTooltipText);

                if (viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null))
                {
                    viewModel.GeneratePostRaceAiSummaryCommand.Execute(null);
                }

                Assert.Equal(0, aiService.AnalyzeCallCount);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies missing AI configuration disables post-race summary commands before execution.
    /// </summary>
    [Fact]
    public void PostRaceAiSummaryCommand_WithMissingAiConfiguration_IsDisabled()
    {
        RunOnStaThread(() =>
        {
            AssertDisabledForConfiguration(
                configure: viewModel => viewModel.AiApiKey = string.Empty,
                expectedReason: "API Key 未配置");
            AssertDisabledForConfiguration(
                configure: viewModel => viewModel.AiBaseUrl = string.Empty,
                expectedReason: "Base URL 未配置");
            AssertDisabledForConfiguration(
                configure: viewModel => viewModel.AiModel = string.Empty,
                expectedReason: "模型未配置");
        });
    }

    /// <summary>
    /// Verifies post-race summary commands re-enable when AI configuration becomes valid.
    /// </summary>
    [Fact]
    public void PostRaceAiSummaryCommand_ReenablesWhenAiConfigurationBecomesValid()
    {
        RunOnStaThread(() =>
        {
            var sessionStateStore = new SessionStateStore(new CarStateStore());
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                sessionStateStore,
                lapAnalyzer: CreateSingleLapAnalyzer());
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";
            viewModel.AiBaseUrl = string.Empty;
            viewModel.AiModel = "test-model";
            ApplyStagedRaceState(sessionStateStore);
            InvokeRefreshPostRaceAiStatus(viewModel, sessionStateStore.CaptureState());
            var canExecuteChangedCount = 0;
            viewModel.GeneratePostRaceAiSummaryCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

            try
            {
                Assert.False(viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null));

                viewModel.AiBaseUrl = "https://api.example.com";

                Assert.True(canExecuteChangedCount > 0);
                Assert.True(viewModel.CanGeneratePostRaceAiSummary);
                Assert.True(viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null));
                Assert.True(viewModel.RegeneratePostRaceAiSummaryCommand.CanExecute(null));
                Assert.Equal("数据可用于生成", viewModel.PostRaceAiDataStatusText);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies the backend AI-disabled guard still prevents analysis when invoked directly.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithAiDisabledGuard_DoesNotCallAiService()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = false;
            viewModel.AiApiKey = "test-key";
            viewModel.AiBaseUrl = "https://api.example.com";
            viewModel.AiModel = "test-model";

            try
            {
                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
                    viewModel,
                    CreateCompletedRaceState(),
                    force: true,
                    bypassDuplicateKey: true);

                Assert.Equal(0, aiService.AnalyzeCallCount);
                Assert.Contains("AI 未启用", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies missing API key preflight clears stale post-race AI report details.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithMissingApiKey_ClearsOldReportDetails()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);
                var logCount = viewModel.AiAnalysisLogs.Count;
                viewModel.AiAnalysisLogs.Add(new LogEntryViewModel
                {
                    Timestamp = "12:00",
                    Category = "AI",
                    Message = "保留的日志"
                });
                viewModel.AiApiKey = string.Empty;

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, CreateCompletedRaceState(), bypassDuplicateKey: true);

                Assert.Equal(0, aiService.AnalyzeCallCount);
                AssertPostRaceAiReportCleared(viewModel, "API Key");
                Assert.Contains(viewModel.AiAnalysisLogs, log => log.Message == "保留的日志");
                Assert.True(viewModel.AiAnalysisLogs.Count >= logCount + 1);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies repeated automatic preflight failures do not refill the AI analysis log.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithRepeatedPreflightFailure_SuppressesDuplicateAutoLog()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";
            var sessionState = CreateCompletedRaceState();

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);
                viewModel.AiApiKey = string.Empty;

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: false);
                InvokeDrainPendingAiAnalysisLogs(viewModel);
                var logCountAfterFirstFailure = viewModel.AiAnalysisLogs.Count;
                var apiKeyFailureLogsAfterFirstFailure = viewModel.AiAnalysisLogs.Count(
                    log => log.Message.Contains("API Key", StringComparison.Ordinal));

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: false);
                InvokeDrainPendingAiAnalysisLogs(viewModel);

                Assert.Equal(logCountAfterFirstFailure, viewModel.AiAnalysisLogs.Count);
                Assert.Equal(
                    apiKeyFailureLogsAfterFirstFailure,
                    viewModel.AiAnalysisLogs.Count(log => log.Message.Contains("API Key", StringComparison.Ordinal)));
                Assert.Equal(0, aiService.AnalyzeCallCount);
                AssertPostRaceAiReportCleared(viewModel, "API Key");

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: true);
                InvokeDrainPendingAiAnalysisLogs(viewModel);

                Assert.Equal(logCountAfterFirstFailure + 1, viewModel.AiAnalysisLogs.Count);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies disabled AI stops generation without clearing stale post-race AI report details.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithAiDisabled_PreservesOldReportDetails()
    {
        RunOnStaThread(() =>
        {
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer());
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);
                var existingLogCount = viewModel.AiAnalysisLogs.Count;
                viewModel.AiEnabled = false;

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
                    viewModel,
                    CreateCompletedRaceState(),
                    force: false,
                    bypassDuplicateKey: false);
                InvokeDrainPendingAiAnalysisLogs(viewModel);

                AssertPostRaceAiReportPreserved(viewModel);
                Assert.Equal(existingLogCount, viewModel.AiAnalysisLogs.Count);

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
                    viewModel,
                    CreateCompletedRaceState(),
                    force: true,
                    bypassDuplicateKey: true);
                InvokeDrainPendingAiAnalysisLogs(viewModel);

                AssertPostRaceAiReportPreserved(viewModel);
                Assert.Contains("AI 未启用", viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
                Assert.DoesNotContain("AI 未启用", viewModel.PostRaceAiFailureReason, StringComparison.Ordinal);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies invalid base URL preflight clears stale post-race AI report details.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithMissingBaseUrl_ClearsOldReportDetails()
    {
        RunOnStaThread(() =>
        {
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer());
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);
                viewModel.AiBaseUrl = string.Empty;

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, CreateCompletedRaceState(), bypassDuplicateKey: true);

                AssertPostRaceAiReportCleared(viewModel, "Base URL");
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies missing model preflight clears stale post-race AI report details.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithMissingModel_ClearsOldReportDetails()
    {
        RunOnStaThread(() =>
        {
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer());
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);
                viewModel.AiModel = string.Empty;

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, CreateCompletedRaceState(), bypassDuplicateKey: true);

                AssertPostRaceAiReportCleared(viewModel, "模型未配置");
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies asynchronous AI failures clear stale post-race AI report details.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithAiFailure_ClearsOldReportDetails()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            aiService.Results.Enqueue(new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = "AI 请求失败：网络错误"
            });
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";

            try
            {
                PrimeSuccessfulPostRaceAiReport(viewModel);

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, CreateCompletedRaceState(), bypassDuplicateKey: true);

                Assert.Equal(1, aiService.AnalyzeCallCount);
                AssertPostRaceAiReportCleared(viewModel, "网络错误");
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies explicit regeneration can rerun AI analysis for the same session lap.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_WithBypassDuplicateKey_RegeneratesSameLap()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            aiService.Results.Enqueue(new AIAnalysisResult { IsSuccess = true, Summary = "第一次报告" });
            aiService.Results.Enqueue(new AIAnalysisResult { IsSuccess = true, Summary = "第二次报告" });
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: new FakeLapAnalyzer([new LapSummary { LapNumber = 22, LapTimeInMs = 88_000 }]),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";
            var sessionState = new SessionState
            {
                SeasonLinkIdentifier = 1,
                WeekendLinkIdentifier = 2,
                SessionLinkIdentifier = 3,
                SessionType = 15,
                HasFinalClassification = true,
                PlayerFinalClassificationLaps = 22,
                PlayerFinalClassificationPosition = 1
            };

            try
            {
                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: false);
                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: false);

                Assert.Equal(1, aiService.AnalyzeCallCount);
                Assert.Equal("第一次报告", viewModel.PostRaceAiReportSummaryText);
                Assert.True(viewModel.PostRaceAiHasReport);

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, bypassDuplicateKey: true);

                Assert.Equal(2, aiService.AnalyzeCallCount);
                Assert.Equal("第二次报告", viewModel.PostRaceAiReportSummaryText);
                Assert.True(viewModel.PostRaceAiHasReport);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    /// <summary>
    /// Verifies regeneration after an automatic report keeps the automatic duplicate key suppressed.
    /// </summary>
    [Fact]
    public void PostRaceAiAnalysis_RegenerationAfterAutomaticReport_SuppressesNextAutomaticTick()
    {
        RunOnStaThread(() =>
        {
            var aiService = new FakeAiAnalysisService();
            aiService.Results.Enqueue(new AIAnalysisResult { IsSuccess = true, Summary = "自动报告" });
            aiService.Results.Enqueue(new AIAnalysisResult { IsSuccess = true, Summary = "重新生成报告" });
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: CreateSingleLapAnalyzer(),
                aiAnalysisService: aiService);
            viewModel.AiEnabled = true;
            viewModel.AiApiKey = "test-key";
            var sessionState = CreateCompletedRaceState();

            try
            {
                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
                    viewModel,
                    sessionState,
                    force: false,
                    bypassDuplicateKey: false);

                Assert.Equal(1, aiService.AnalyzeCallCount);
                Assert.Equal("自动报告", viewModel.PostRaceAiReportSummaryText);

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
                    viewModel,
                    sessionState,
                    force: true,
                    bypassDuplicateKey: true);

                Assert.Equal(2, aiService.AnalyzeCallCount);
                Assert.Equal("重新生成报告", viewModel.PostRaceAiReportSummaryText);

                InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
                    viewModel,
                    sessionState,
                    force: false,
                    bypassDuplicateKey: false);

                Assert.Equal(2, aiService.AnalyzeCallCount);
                Assert.Equal("重新生成报告", viewModel.PostRaceAiReportSummaryText);
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

    /// <summary>
    /// Verifies same-lap official timing refinements update summary storage without repeating lap side effects.
    /// </summary>
    [Fact]
    public void RefreshCentralState_SameLapRefinement_DoesNotRepeatLapSideEffects()
    {
        RunOnStaThread(() =>
        {
            var storage = new FakeStoragePersistenceService();
            var lapAnalyzer = new MutableLapAnalyzer(
            [
                new LapSummary { LapNumber = 4, LapTimeInMs = 91_000, AverageSpeedKph = 205, ClosedAt = DateTimeOffset.UtcNow }
            ],
            [
                new LapSample { LapNumber = 4, SampledAt = DateTimeOffset.UtcNow, FrameIdentifier = 1 },
                new LapSample { LapNumber = 4, SampledAt = DateTimeOffset.UtcNow.AddSeconds(1), FrameIdentifier = 2 }
            ]);
            var viewModel = CreateDashboardViewModel(
                new FakePacketDispatcher(),
                lapAnalyzer: lapAnalyzer,
                storagePersistenceService: storage);

            try
            {
                InvokeRefreshCentralState(viewModel);
                lapAnalyzer.Laps =
                [
                    new LapSummary
                    {
                        LapNumber = 4,
                        LapTimeInMs = 90_500,
                        Sector1TimeInMs = 30_000,
                        Sector2TimeInMs = 30_200,
                        Sector3TimeInMs = 30_300,
                        AverageSpeedKph = 205,
                        FuelUsedLitres = 1.4f,
                        ClosedAt = DateTimeOffset.UtcNow.AddSeconds(1)
                    }
                ];
                InvokeRefreshCentralState(viewModel);

                Assert.Equal(2, storage.EnqueuedLapSummaries.Count);
                var sampleWrite = Assert.Single(storage.EnqueuedLapSampleWrites);
                Assert.Equal(4, sampleWrite.LapNumber);
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
        FakeStoragePersistenceService? storagePersistenceService = null,
        FakeAiAnalysisService? aiAnalysisService = null)
    {
        var ttsQueue = new TtsQueue(new FakeTtsService(), new TtsOptions());
        return new DashboardViewModel(
            new FakeUdpListener(),
            dispatcher,
            sessionStateStore ?? new SessionStateStore(new CarStateStore()),
            lapAnalyzer ?? new LapAnalyzer(),
            new EventDetectionService(),
            aiAnalysisService ?? new FakeAiAnalysisService(),
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

    private static void InvokeUpdatePostRaceAiReportDetails(
        DashboardViewModel viewModel,
        AIAnalysisResult result,
        LapSummary lastLap)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "UpdatePostRaceAiReportDetails",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, new object[] { result, lastLap, DateTimeOffset.UtcNow });
    }

    private static void InvokeRefreshPostRaceAiStatus(
        DashboardViewModel viewModel,
        SessionState sessionState)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "RefreshPostRaceAiStatus",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, new object[] { sessionState });
    }

    private static void InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
        DashboardViewModel viewModel,
        SessionState sessionState,
        bool bypassDuplicateKey)
    {
        InvokeTriggerPostRaceAiAnalysisIfReadyAsync(viewModel, sessionState, force: true, bypassDuplicateKey);
    }

    private static void InvokeTriggerPostRaceAiAnalysisIfReadyAsync(
        DashboardViewModel viewModel,
        SessionState sessionState,
        bool force,
        bool bypassDuplicateKey)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "TriggerPostRaceAiAnalysisIfReadyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            viewModel,
            new object?[] { sessionState, sessionState.PlayerCar, force, bypassDuplicateKey }));
        task.GetAwaiter().GetResult();
    }

    private static void InvokeDrainPendingAiAnalysisLogs(DashboardViewModel viewModel)
    {
        var method = typeof(DashboardViewModel).GetMethod(
            "DrainPendingAiAnalysisLogs",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(viewModel, null);
    }

    private static void PrimeSuccessfulPostRaceAiReport(DashboardViewModel viewModel)
    {
        InvokeUpdatePostRaceAiReportDetails(
            viewModel,
            new AIAnalysisResult
            {
                IsSuccess = true,
                Summary = "旧比赛结论",
                KeyProblems = ["旧主要问题"],
                StrategyReview = "旧策略回顾",
                TyreReview = "旧轮胎表现",
                ErsFuelReview = "旧 ERS / 燃油",
                OpponentReview = "旧对手攻防",
                Improvements = ["旧改进建议"]
            },
            new LapSummary { LapNumber = 21 });

        Assert.True(viewModel.PostRaceAiHasReport);
        Assert.Equal("旧比赛结论", viewModel.PostRaceAiReportSummaryText);
    }

    private static void AssertDisabledForConfiguration(Action<DashboardViewModel> configure, string expectedReason)
    {
        var sessionStateStore = new SessionStateStore(new CarStateStore());
        var viewModel = CreateDashboardViewModel(
            new FakePacketDispatcher(),
            sessionStateStore,
            lapAnalyzer: CreateSingleLapAnalyzer());
        viewModel.AiEnabled = true;
        viewModel.AiApiKey = "test-key";
        viewModel.AiBaseUrl = "https://api.example.com";
        viewModel.AiModel = "test-model";

        try
        {
            ApplyStagedRaceState(sessionStateStore);
            configure(viewModel);

            InvokeRefreshPostRaceAiStatus(viewModel, sessionStateStore.CaptureState());

            Assert.False(viewModel.CanGeneratePostRaceAiSummary);
            Assert.False(viewModel.GeneratePostRaceAiSummaryCommand.CanExecute(null));
            Assert.False(viewModel.RegeneratePostRaceAiSummaryCommand.CanExecute(null));
            Assert.Contains(expectedReason, viewModel.PostRaceAiDataStatusText, StringComparison.Ordinal);
            Assert.Contains(expectedReason, viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
            Assert.Contains(expectedReason, viewModel.PostRaceAiSummaryCommandTooltipText, StringComparison.Ordinal);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    private static void ApplyStagedRaceState(SessionStateStore sessionStateStore)
    {
        var aggregator = new StateAggregator(sessionStateStore);
        aggregator.ApplyPacket(CreateParsedPacket(CreateRaceSessionPacket(), playerCarIndex: 0));
        aggregator.ApplyPacket(CreateParsedPacket(
            new EventPacket("CHQF", EventCode.ChequeredFlag, new EmptyEventDetail()),
            playerCarIndex: 0));
    }

    private static void AssertPostRaceAiReportCleared(DashboardViewModel viewModel, string expectedReason)
    {
        Assert.False(viewModel.PostRaceAiHasReport);
        Assert.Equal("最近分析：暂无", viewModel.PostRaceAiLastAnalysisText);
        Assert.Equal("暂无 AI 分析报告", viewModel.PostRaceAiReportSummaryText);
        Assert.Equal("等待完赛数据", viewModel.PostRaceAiKeyProblemsText);
        Assert.Equal("等待完赛数据", viewModel.PostRaceAiStrategyReviewText);
        Assert.Equal("等待完赛数据", viewModel.PostRaceAiTyreReviewText);
        Assert.Equal("等待完赛数据", viewModel.PostRaceAiErsFuelReviewText);
        Assert.Equal("等待完赛数据", viewModel.PostRaceAiOpponentReviewText);
        Assert.Equal("等待完赛数据", viewModel.PostRaceAiImprovementsText);
        Assert.Contains(expectedReason, viewModel.PostRaceAiStatusText, StringComparison.Ordinal);
        Assert.Contains(expectedReason, viewModel.PostRaceAiFailureReason, StringComparison.Ordinal);
    }

    private static void AssertPostRaceAiReportPreserved(DashboardViewModel viewModel)
    {
        Assert.True(viewModel.PostRaceAiHasReport);
        Assert.Equal("旧比赛结论", viewModel.PostRaceAiReportSummaryText);
        Assert.Equal("旧主要问题", viewModel.PostRaceAiKeyProblemsText);
        Assert.Equal("旧策略回顾", viewModel.PostRaceAiStrategyReviewText);
        Assert.Equal("旧轮胎表现", viewModel.PostRaceAiTyreReviewText);
        Assert.Equal("旧 ERS / 燃油", viewModel.PostRaceAiErsFuelReviewText);
        Assert.Equal("旧对手攻防", viewModel.PostRaceAiOpponentReviewText);
        Assert.Equal("旧改进建议", viewModel.PostRaceAiImprovementsText);
    }

    private static SessionState CreateCompletedRaceState()
    {
        return new SessionState
        {
            SeasonLinkIdentifier = 1,
            WeekendLinkIdentifier = 2,
            SessionLinkIdentifier = 3,
            SessionType = 15,
            HasFinalClassification = true,
            PlayerFinalClassificationLaps = 22,
            PlayerFinalClassificationPosition = 1
        };
    }

    private static SessionPacket CreateRaceSessionPacket()
    {
        return new SessionPacket(
            Weather: 2,
            TrackTemperature: 31,
            AirTemperature: 24,
            TotalLaps: 29,
            TrackLength: 5400,
            SessionType: 15,
            TrackId: 10,
            Formula: 0,
            SessionTimeLeft: 1800,
            SessionDuration: 3600,
            PitSpeedLimit: 80,
            GamePaused: false,
            IsSpectating: false,
            SpectatorCarIndex: 0,
            SliProNativeSupport: false,
            NumMarshalZones: 0,
            MarshalZones: Array.Empty<MarshalZoneData>(),
            SafetyCarStatus: 0,
            NetworkGame: false,
            NumWeatherForecastSamples: 0,
            WeatherForecastSamples: Array.Empty<WeatherForecastSampleData>(),
            ForecastAccuracy: 0,
            AiDifficulty: 80,
            SeasonLinkIdentifier: 1,
            WeekendLinkIdentifier: 2,
            SessionLinkIdentifier: 3,
            PitStopWindowIdealLap: 0,
            PitStopWindowLatestLap: 0,
            PitStopRejoinPosition: 0,
            SteeringAssist: false,
            BrakingAssist: 0,
            GearboxAssist: 0,
            PitAssist: false,
            PitReleaseAssist: false,
            ErsAssist: false,
            DrsAssist: false,
            DynamicRacingLine: 0,
            DynamicRacingLineType: 0,
            GameMode: 0,
            RuleSet: 0,
            TimeOfDay: 0,
            SessionLength: 0,
            SpeedUnitsLeadPlayer: 0,
            TemperatureUnitsLeadPlayer: 0,
            SpeedUnitsSecondaryPlayer: 0,
            TemperatureUnitsSecondaryPlayer: 0,
            NumSafetyCarPeriods: 0,
            NumVirtualSafetyCarPeriods: 0,
            NumRedFlagPeriods: 0,
            EqualCarPerformance: true,
            RecoveryMode: 0,
            FlashbackLimit: 0,
            SurfaceType: 0,
            LowFuelMode: false,
            RaceStarts: true,
            TyreTemperature: true,
            PitLaneTyreSim: false,
            CarDamage: 0,
            CarDamageRate: 0,
            Collisions: 0,
            CollisionsOffForFirstLapOnly: false,
            MpUnsafePitRelease: false,
            MpOffForGriefing: false,
            CornerCuttingStringency: 0,
            ParcFermeRules: true,
            PitStopExperience: 0,
            SafetyCar: 0,
            SafetyCarExperience: 0,
            FormationLap: false,
            FormationLapExperience: false,
            RedFlags: 0,
            AffectsLicenceLevelSolo: false,
            AffectsLicenceLevelMp: false,
            NumSessionsInWeekend: 0,
            WeekendStructure: Array.Empty<byte>(),
            Sector2LapDistanceStart: 0f,
            Sector3LapDistanceStart: 0f);
    }

    private static FakeLapAnalyzer CreateSingleLapAnalyzer()
    {
        return new FakeLapAnalyzer([new LapSummary { LapNumber = 22, LapTimeInMs = 88_000 }]);
    }

    private static ParsedPacket CreateParsedPacket(IUdpPacket packet, byte playerCarIndex)
    {
        var header = new PacketHeader(
            PacketFormat: 2025,
            GameYear: 25,
            GameMajorVersion: 1,
            GameMinorVersion: 0,
            PacketVersion: 1,
            RawPacketId: GetPacketId(packet),
            SessionUid: 123UL,
            SessionTime: 0,
            FrameIdentifier: 1,
            OverallFrameIdentifier: 1,
            PlayerCarIndex: playerCarIndex,
            SecondaryPlayerCarIndex: 255);
        var datagram = new UdpDatagram(Array.Empty<byte>(), new IPEndPoint(IPAddress.Loopback, 20777), DateTimeOffset.UtcNow);
        return new ParsedPacket((PacketId)header.RawPacketId, header, packet, datagram);
    }

    private static byte GetPacketId(IUdpPacket packet)
    {
        return packet switch
        {
            SessionPacket => (byte)PacketId.Session,
            EventPacket => (byte)PacketId.Event,
            CarTelemetryPacket => (byte)PacketId.CarTelemetry,
            _ => throw new ArgumentOutOfRangeException(nameof(packet))
        };
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
        public Queue<AIAnalysisResult> Results { get; } = new();

        public int AnalyzeCallCount { get; private set; }

        public Task<AIAnalysisResult> AnalyzeAsync(
            AIAnalysisContext context,
            AISettings settings,
            CancellationToken cancellationToken = default)
        {
            AnalyzeCallCount++;
            return Task.FromResult(Results.TryDequeue(out var result) ? result : new AIAnalysisResult());
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

        public Task SaveRaceWeekendTyrePlanAsync(RaceWeekendTyrePlan plan, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveUdpRawLogOptionsAsync(UdpRawLogOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveVoiceAiOptionsAsync(VoiceAiOptions options, CancellationToken cancellationToken = default)
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

        public List<LapSampleWrite> EnqueuedLapSampleWrites { get; } = [];

        public void ObserveParsedPacket(ParsedPacket parsedPacket)
        {
        }

        public void EnqueueLapSummary(LapSummary lapSummary)
        {
            EnqueuedLapSummaries.Add(lapSummary);
        }

        public void EnqueueLapSamples(int lapNumber, IReadOnlyList<LapSample> lapSamples)
        {
            EnqueuedLapSampleWrites.Add(new LapSampleWrite(lapNumber, lapSamples.ToArray()));
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

    private sealed record LapSampleWrite(int LapNumber, IReadOnlyList<LapSample> Samples);

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

        public IReadOnlyList<LapSample> CaptureCompletedLapSamples(int lapNumber)
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

    private sealed class MutableLapAnalyzer(
        IReadOnlyList<LapSummary> laps,
        IReadOnlyList<LapSample> completedSamples) : ILapAnalyzer
    {
        public IReadOnlyList<LapSummary> Laps { get; set; } = laps;

        public void Observe(ParsedPacket parsedPacket, SessionState sessionState)
        {
        }

        public void ResetForSession(ulong sessionUid)
        {
        }

        public IReadOnlyList<LapSummary> CaptureAllLaps()
        {
            return Laps;
        }

        public IReadOnlyList<LapSample> CaptureCurrentLapSamples()
        {
            return Array.Empty<LapSample>();
        }

        public IReadOnlyList<LapSample> CaptureCompletedLapSamples(int lapNumber)
        {
            return lapNumber == 4 ? completedSamples : Array.Empty<LapSample>();
        }

        public IReadOnlyList<LapSummary> CaptureRecentLaps(int maxCount)
        {
            return Laps.Take(maxCount).Reverse().ToArray();
        }

        public LapSummary? CaptureBestLap()
        {
            return Laps.OrderBy(lap => lap.LapTimeInMs).FirstOrDefault();
        }

        public LapSummary? CaptureLastLap()
        {
            return Laps.LastOrDefault();
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
