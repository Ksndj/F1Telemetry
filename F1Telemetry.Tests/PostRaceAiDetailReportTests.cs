using F1Telemetry.App.ViewModels;
using F1Telemetry.Storage.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies post-race AI detail reports are shown separately from speech text.
/// </summary>
public sealed class PostRaceAiDetailReportTests
{
    /// <summary>
    /// Verifies stored race-engineer detail JSON is projected into the review UI row.
    /// </summary>
    [Fact]
    public void FromStoredRaceEngineerReport_ShowsDetailedReportAndShortSpeechSeparately()
    {
        var row = PostRaceReviewAiReportRowViewModel.FromStoredRaceEngineerReport(
            new StoredRaceEngineerReport
            {
                LapNumber = 29,
                ReportType = "post-race-ai",
                Summary = "比赛后段掉速",
                SpokenText = "比赛结束，稍后看报告。",
                DetailJson = """
                {
                  "summary": "比赛后段掉速",
                  "keyProblems": ["胎磨后段扩大", "ERS 使用偏激进"],
                  "strategyReview": "进站窗口偏晚",
                  "tyreReview": "轮胎后段掉速明显",
                  "ersFuelReview": "ERS 管理需要更早省电",
                  "opponentReview": "后车压力在最后阶段增大",
                  "improvements": ["提前两圈观察胎磨", "直道保留 ERS", "防守前稳住出弯"]
                }
                """,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

        Assert.Equal("比赛结束，稍后看报告。", row.TtsText);
        Assert.Contains("比赛结论：比赛后段掉速", row.DetailReportText, StringComparison.Ordinal);
        Assert.Contains("主要问题：胎磨后段扩大；ERS 使用偏激进", row.DetailReportText, StringComparison.Ordinal);
        Assert.Contains("下次改进：提前两圈观察胎磨；直道保留 ERS；防守前稳住出弯", row.DetailReportText, StringComparison.Ordinal);
        Assert.DoesNotContain(row.DetailReportText, row.TtsText, StringComparison.Ordinal);
    }
}
