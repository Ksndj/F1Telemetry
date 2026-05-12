using System.Windows;

namespace F1Telemetry.App.Services;

/// <summary>
/// Confirms history session deletion with a standard WPF message box.
/// </summary>
public sealed class MessageBoxHistorySessionDeletionConfirmationService : IHistorySessionDeletionConfirmationService
{
    /// <inheritdoc />
    public Task<bool> ConfirmDeleteAsync(
        HistorySessionDeletionConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        var result = MessageBox.Show(
            $"确定永久删除这个历史会话吗？\n\n{request.SessionSummary}\nSessionUid: {request.SessionUid}\n\n对应单圈、事件和 AI 报告也会一起删除。",
            "删除历史会话",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
