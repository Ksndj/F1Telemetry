using System.Text.Json;
using F1Telemetry.AI.Interfaces;
using F1Telemetry.AI.Models;

namespace F1Telemetry.AI.Services;

/// <summary>
/// Coordinates prompt creation, DeepSeek requests, and JSON result parsing for lap analysis.
/// </summary>
public sealed class DeepSeekAnalysisService : IAIAnalysisService
{
    private readonly DeepSeekClient _deepSeekClient;
    private readonly PromptBuilder _promptBuilder;

    /// <summary>
    /// Initializes a new AI analysis service.
    /// </summary>
    public DeepSeekAnalysisService(DeepSeekClient deepSeekClient, PromptBuilder promptBuilder)
    {
        _deepSeekClient = deepSeekClient ?? throw new ArgumentNullException(nameof(deepSeekClient));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
    }

    /// <inheritdoc />
    public async Task<AIAnalysisResult> AnalyzeAsync(
        AIAnalysisContext context,
        AISettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.AiEnabled)
        {
            return new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = "AI analysis is disabled."
            };
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = "API Key is required."
            };
        }

        try
        {
            var prompt = _promptBuilder.BuildMessages(context);
            var request = new DeepSeekChatCompletionRequest
            {
                Model = string.IsNullOrWhiteSpace(settings.Model) ? "deepseek-chat" : settings.Model,
                Messages =
                [
                    new DeepSeekChatMessage("system", prompt.SystemMessage),
                    new DeepSeekChatMessage("user", prompt.UserMessage)
                ]
            };

            var content = await _deepSeekClient.CreateChatCompletionAsync(request, settings, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AIAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = "AI response content was empty."
                };
            }

            var result = JsonSerializer.Deserialize<AIAnalysisResult>(content);
            if (result is null)
            {
                return new AIAnalysisResult
                {
                    IsSuccess = false,
                    ErrorMessage = "AI response JSON could not be parsed."
                };
            }

            return result with
            {
                IsSuccess = true,
                ErrorMessage = string.Empty,
                Summary = string.IsNullOrWhiteSpace(result.Summary) ? "-" : result.Summary,
                TyreAdvice = string.IsNullOrWhiteSpace(result.TyreAdvice) ? "-" : result.TyreAdvice,
                FuelAdvice = string.IsNullOrWhiteSpace(result.FuelAdvice) ? "-" : result.FuelAdvice,
                TrafficAdvice = string.IsNullOrWhiteSpace(result.TrafficAdvice) ? "-" : result.TrafficAdvice,
                TtsText = string.IsNullOrWhiteSpace(result.TtsText) ? "-" : result.TtsText
            };
        }
        catch (Exception ex)
        {
            return new AIAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
