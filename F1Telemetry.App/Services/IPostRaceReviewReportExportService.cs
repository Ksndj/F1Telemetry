namespace F1Telemetry.App.Services;

/// <summary>
/// Saves generated post-race review reports to a user-selected destination.
/// </summary>
public interface IPostRaceReviewReportExportService
{
    /// <summary>
    /// Saves the generated report content.
    /// </summary>
    /// <param name="request">The report export request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PostRaceReviewReportExportResult> ExportAsync(
        PostRaceReviewReportExportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identifies a supported post-race review export format.
/// </summary>
public enum PostRaceReviewReportFormat
{
    /// <summary>
    /// Exports a Markdown report.
    /// </summary>
    Markdown,

    /// <summary>
    /// Exports a JSON report.
    /// </summary>
    Json
}

/// <summary>
/// Represents generated report content ready to be saved.
/// </summary>
/// <param name="Format">The report format.</param>
/// <param name="SuggestedFileName">The suggested output file name.</param>
/// <param name="Content">The report content.</param>
public sealed record PostRaceReviewReportExportRequest(
    PostRaceReviewReportFormat Format,
    string SuggestedFileName,
    string Content);

/// <summary>
/// Represents the result of a report export attempt.
/// </summary>
/// <param name="Exported">Whether the report was saved.</param>
/// <param name="FilePath">The saved file path when available.</param>
public sealed record PostRaceReviewReportExportResult(bool Exported, string? FilePath);
