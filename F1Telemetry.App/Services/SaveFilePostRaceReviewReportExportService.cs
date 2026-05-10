using System.IO;
using System.Text;
using Microsoft.Win32;

namespace F1Telemetry.App.Services;

/// <summary>
/// Saves post-race review reports through the standard Windows save-file dialog.
/// </summary>
public sealed class SaveFilePostRaceReviewReportExportService : IPostRaceReviewReportExportService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public async Task<PostRaceReviewReportExportResult> ExportAsync(
        PostRaceReviewReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            CheckPathExists = true,
            DefaultExt = request.Format == PostRaceReviewReportFormat.Markdown ? ".md" : ".json",
            FileName = request.SuggestedFileName,
            Filter = request.Format == PostRaceReviewReportFormat.Markdown
                ? "Markdown report (*.md)|*.md"
                : "JSON report (*.json)|*.json",
            OverwritePrompt = true,
            Title = "导出历史会话复盘报告"
        };

        if (dialog.ShowDialog() != true)
        {
            return new PostRaceReviewReportExportResult(false, null);
        }

        await File.WriteAllTextAsync(dialog.FileName, request.Content, Utf8WithoutBom, cancellationToken);
        return new PostRaceReviewReportExportResult(true, dialog.FileName);
    }
}
