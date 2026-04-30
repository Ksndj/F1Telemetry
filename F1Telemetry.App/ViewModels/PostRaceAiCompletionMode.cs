namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Defines how the live dashboard decides when a race is ready for the post-race AI summary.
/// </summary>
public enum PostRaceAiCompletionMode
{
    /// <summary>
    /// Uses UDP final-classification evidence before generating the post-race summary.
    /// </summary>
    Auto,

    /// <summary>
    /// Keeps the current race data staged and suppresses AI summary upload.
    /// </summary>
    Hold,

    /// <summary>
    /// Lets the user mark the current race as complete and generate the summary manually.
    /// </summary>
    ForceComplete
}
