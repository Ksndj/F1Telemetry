# DeepSeek Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a minimal DeepSeek-backed lap-analysis flow with persisted local settings, fixed-JSON output, and WPF dashboard integration that triggers once per completed player lap.

**Architecture:** Keep AI concerns inside `F1Telemetry.AI` with small focused types for settings, prompt construction, HTTP access, and analysis orchestration. Let `F1Telemetry.App` only bind settings, detect new completed laps from `ILapAnalyzer`, invoke the AI service on a background thread, and surface result summaries in the existing AI log area.

**Tech Stack:** .NET 8, WPF, MVVM, `HttpClient`, `System.Text.Json`, xUnit

---

### Task 1: Define AI contracts and context models

**Files:**
- Create: `F1Telemetry.AI/Models/AIAnalysisResult.cs`
- Create: `F1Telemetry.AI/Models/AIAnalysisContext.cs`
- Create: `F1Telemetry.AI/Models/AISettings.cs`
- Create: `F1Telemetry.AI/Interfaces/IAIAnalysisService.cs`
- Modify: `F1Telemetry.AI/F1Telemetry.AI.csproj`
- Test: `F1Telemetry.Tests/PromptBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void BuildMessages_IncludesFixedJsonContract()
{
    var builder = new PromptBuilder();
    var context = new AIAnalysisContext
    {
        LatestLap = new LapSummary { LapNumber = 12, LapTimeInMs = 90500, IsValid = true },
        BestLap = new LapSummary { LapNumber = 8, LapTimeInMs = 89900, IsValid = true },
        RecentLaps = new[]
        {
            new LapSummary { LapNumber = 12, LapTimeInMs = 90500, IsValid = true },
            new LapSummary { LapNumber = 11, LapTimeInMs = 91200, IsValid = true }
        },
        CurrentFuelRemainingLaps = 4.2f,
        CurrentTyre = "V16 / A19",
        RecentEvents = new[] { "Front car pitted." }
    };

    var prompt = builder.BuildMessages(context);

    Assert.Contains("summary", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("tyreAdvice", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("fuelAdvice", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("trafficAdvice", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("ttsText", prompt.SystemMessage, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter BuildMessages_IncludesFixedJsonContract`
Expected: FAIL because `PromptBuilder` and AI context/result types do not exist yet

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed record AIAnalysisResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string Summary { get; init; } = "-";
    public string TyreAdvice { get; init; } = "-";
    public string FuelAdvice { get; init; } = "-";
    public string TrafficAdvice { get; init; } = "-";
    public string TtsText { get; init; } = "-";
}
```

```csharp
public sealed record AIAnalysisContext
{
    public LapSummary? LatestLap { get; init; }
    public LapSummary? BestLap { get; init; }
    public IReadOnlyList<LapSummary> RecentLaps { get; init; } = Array.Empty<LapSummary>();
    public float? CurrentFuelRemainingLaps { get; init; }
    public float? CurrentFuelInTank { get; init; }
    public float? CurrentErsStoreEnergy { get; init; }
    public string CurrentTyre { get; init; } = "-";
    public byte? CurrentTyreAgeLaps { get; init; }
    public ushort? GapToFrontInMs { get; init; }
    public ushort? GapToBehindInMs { get; init; }
    public IReadOnlyList<string> RecentEvents { get; init; } = Array.Empty<string>();
}
```

```csharp
public sealed record AISettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.deepseek.com";
    public string Model { get; init; } = "deepseek-chat";
    public bool AiEnabled { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 10;
}
```

```csharp
public interface IAIAnalysisService
{
    Task<AIAnalysisResult> AnalyzeAsync(AIAnalysisContext context, AISettings settings, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter BuildMessages_IncludesFixedJsonContract`
Expected: PASS after `PromptBuilder` is added in Task 2

- [ ] **Step 5: Commit**

```bash
git add F1Telemetry.AI F1Telemetry.Tests
git commit -m "feat: add ai analysis contracts"
```

### Task 2: Add prompt builder and fixed JSON request shape

**Files:**
- Create: `F1Telemetry.AI/Models/AIPromptMessages.cs`
- Create: `F1Telemetry.AI/Services/PromptBuilder.cs`
- Test: `F1Telemetry.Tests/PromptBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void BuildMessages_UsesLapAndStateSummariesInsteadOfRawPackets()
{
    var builder = new PromptBuilder();
    var context = new AIAnalysisContext
    {
        LatestLap = new LapSummary { LapNumber = 14, LapTimeInMs = 91000, IsValid = true },
        BestLap = new LapSummary { LapNumber = 10, LapTimeInMs = 90300, IsValid = true },
        RecentLaps = new[]
        {
            new LapSummary { LapNumber = 14, LapTimeInMs = 91000, IsValid = true },
            new LapSummary { LapNumber = 13, LapTimeInMs = 91500, IsValid = false }
        },
        CurrentFuelInTank = 8.4f,
        CurrentFuelRemainingLaps = 5.1f,
        CurrentErsStoreEnergy = 2250000f,
        CurrentTyre = "V16 / A19",
        CurrentTyreAgeLaps = 7,
        GapToFrontInMs = 1250,
        GapToBehindInMs = 980,
        RecentEvents = new[] { "Rear car pitted." }
    };

    var prompt = builder.BuildMessages(context);

    Assert.Contains("Lap 14", prompt.UserMessage, StringComparison.Ordinal);
    Assert.Contains("Rear car pitted.", prompt.UserMessage, StringComparison.Ordinal);
    Assert.DoesNotContain("packet", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("udp", prompt.UserMessage, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter BuildMessages_UsesLapAndStateSummariesInsteadOfRawPackets`
Expected: FAIL because `PromptBuilder.BuildMessages` is not implemented yet

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed record AIPromptMessages
{
    public string SystemMessage { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
}
```

```csharp
public sealed class PromptBuilder
{
    public AIPromptMessages BuildMessages(AIAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new AIPromptMessages
        {
            SystemMessage =
                "You are an F1 race engineer. Return only JSON with keys: summary, tyreAdvice, fuelAdvice, trafficAdvice, ttsText.",
            UserMessage =
                $"Latest lap: Lap {context.LatestLap?.LapNumber}, time {context.LatestLap?.LapTimeInMs}. " +
                $"Best lap: {context.BestLap?.LapTimeInMs}. " +
                $"Fuel laps: {context.CurrentFuelRemainingLaps}. ERS: {context.CurrentErsStoreEnergy}. " +
                $"Tyre: {context.CurrentTyre}. Events: {string.Join(\" | \", context.RecentEvents)}."
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter PromptBuilderTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add F1Telemetry.AI F1Telemetry.Tests
git commit -m "feat: add ai prompt builder"
```

### Task 3: Add local JSON settings store with safe fallback

**Files:**
- Create: `F1Telemetry.AI/Interfaces/IAISettingsStore.cs`
- Create: `F1Telemetry.AI/Services/AISettingsStore.cs`
- Test: `F1Telemetry.Tests/AISettingsStoreTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task LoadAsync_MissingFile_ReturnsDefaults()
{
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    var store = new AISettingsStore(root);

    var settings = await store.LoadAsync();

    Assert.Equal("https://api.deepseek.com", settings.BaseUrl);
    Assert.Equal("deepseek-chat", settings.Model);
    Assert.False(settings.AiEnabled);
    Assert.Equal(10, settings.RequestTimeoutSeconds);
}
```

```csharp
[Fact]
public async Task LoadAsync_InvalidJson_ReturnsDefaults()
{
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "F1Telemetry"));
    await File.WriteAllTextAsync(Path.Combine(root, "F1Telemetry", "settings.json"), "{not-json}");
    var store = new AISettingsStore(root);

    var settings = await store.LoadAsync();

    Assert.Equal("https://api.deepseek.com", settings.BaseUrl);
    Assert.Equal(10, settings.RequestTimeoutSeconds);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter AISettingsStoreTests`
Expected: FAIL because settings store types do not exist yet

- [ ] **Step 3: Write minimal implementation**

```csharp
public interface IAISettingsStore
{
    Task<AISettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AISettings settings, CancellationToken cancellationToken = default);
}
```

```csharp
public sealed class AISettingsStore : IAISettingsStore
{
    private readonly string _settingsPath;

    public AISettingsStore(string? localAppDataRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(localAppDataRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localAppDataRoot;
        _settingsPath = Path.Combine(root, "F1Telemetry", "settings.json");
    }

    public async Task<AISettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AISettings();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<AISettings>(stream, cancellationToken: cancellationToken)
                ?? new AISettings();
        }
        catch
        {
            return new AISettings();
        }
    }

    public async Task SaveAsync(AISettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter AISettingsStoreTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add F1Telemetry.AI F1Telemetry.Tests
git commit -m "feat: add ai settings persistence"
```

### Task 4: Add DeepSeek client and analysis service

**Files:**
- Create: `F1Telemetry.AI/Models/DeepSeekChatCompletionRequest.cs`
- Create: `F1Telemetry.AI/Models/DeepSeekChatCompletionResponse.cs`
- Create: `F1Telemetry.AI/Services/DeepSeekClient.cs`
- Create: `F1Telemetry.AI/Services/DeepSeekAnalysisService.cs`
- Modify: `F1Telemetry.AI/Services/AiRaceEngineerService.cs`
- Test: `F1Telemetry.Tests/DeepSeekAnalysisServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task AnalyzeAsync_MissingApiKey_ReturnsFailureResult()
{
    var service = new DeepSeekAnalysisService(
        new DeepSeekClient(new HttpClient(new StubHandler(_ => throw new InvalidOperationException("should not call")))),
        new PromptBuilder());

    var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = "" });

    Assert.False(result.IsSuccess);
    Assert.Contains("API Key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
}
```

```csharp
[Fact]
public async Task AnalyzeAsync_ValidJson_ParsesFixedResult()
{
    var json = "{\"choices\":[{\"message\":{\"content\":\"{\\\"summary\\\":\\\"pace ok\\\",\\\"tyreAdvice\\\":\\\"stay out\\\",\\\"fuelAdvice\\\":\\\"target +0.2\\\",\\\"trafficAdvice\\\":\\\"watch front gap\\\",\\\"ttsText\\\":\\\"pace is okay\\\"}\"}}]}";
    var client = new DeepSeekClient(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    })));
    var service = new DeepSeekAnalysisService(client, new PromptBuilder());

    var result = await service.AnalyzeAsync(new AIAnalysisContext(), new AISettings { AiEnabled = true, ApiKey = "secret" });

    Assert.True(result.IsSuccess);
    Assert.Equal("pace ok", result.Summary);
    Assert.Equal("pace is okay", result.TtsText);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter DeepSeekAnalysisServiceTests`
Expected: FAIL because client and analysis service do not exist yet

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class DeepSeekClient
{
    private readonly HttpClient _httpClient;

    public DeepSeekClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> CreateChatCompletionAsync(DeepSeekChatCompletionRequest request, AISettings settings, CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(settings.BaseUrl);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        requestMessage.Content = JsonContent.Create(request);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds <= 0 ? 10 : settings.RequestTimeoutSeconds));
        using var response = await _httpClient.SendAsync(requestMessage, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<DeepSeekChatCompletionResponse>(cancellationToken: timeoutCts.Token);
        return envelope?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    internal static string NormalizeBaseUrl(string? baseUrl) =>
        (baseUrl ?? "https://api.deepseek.com").Trim().TrimEnd('/').Replace("/chat/completions", string.Empty, StringComparison.OrdinalIgnoreCase);
}
```

```csharp
public sealed class DeepSeekAnalysisService : IAIAnalysisService
{
    private readonly DeepSeekClient _client;
    private readonly PromptBuilder _promptBuilder;

    public DeepSeekAnalysisService(DeepSeekClient client, PromptBuilder promptBuilder)
    {
        _client = client;
        _promptBuilder = promptBuilder;
    }

    public async Task<AIAnalysisResult> AnalyzeAsync(AIAnalysisContext context, AISettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.AiEnabled)
        {
            return new AIAnalysisResult { IsSuccess = false, ErrorMessage = "AI is disabled." };
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new AIAnalysisResult { IsSuccess = false, ErrorMessage = "API Key is required." };
        }

        try
        {
            var prompt = _promptBuilder.BuildMessages(context);
            var content = await _client.CreateChatCompletionAsync(new DeepSeekChatCompletionRequest
            {
                Model = string.IsNullOrWhiteSpace(settings.Model) ? "deepseek-chat" : settings.Model,
                Messages = new[]
                {
                    new DeepSeekChatMessage("system", prompt.SystemMessage),
                    new DeepSeekChatMessage("user", prompt.UserMessage)
                }
            }, settings, cancellationToken);

            var result = JsonSerializer.Deserialize<AIAnalysisResult>(content);
            return result is null
                ? new AIAnalysisResult { IsSuccess = false, ErrorMessage = "AI response JSON was empty." }
                : result with { IsSuccess = true, ErrorMessage = null };
        }
        catch (Exception ex)
        {
            return new AIAnalysisResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter DeepSeekAnalysisServiceTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add F1Telemetry.AI F1Telemetry.Tests
git commit -m "feat: add deepseek ai analysis service"
```

### Task 5: Wire AI settings and lap-trigger flow into the WPF dashboard

**Files:**
- Modify: `F1Telemetry.App/App.xaml.cs`
- Modify: `F1Telemetry.App/ViewModels/DashboardViewModel.cs`
- Modify: `F1Telemetry.App/MainWindow.xaml`
- Possibly create: `F1Telemetry.App/ViewModels/AISettingsViewModel.cs`
- Test: `F1Telemetry.Tests/DashboardAITriggerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task RefreshCentralState_NewClosedLap_TriggersAnalysisOnce()
{
    var fakeAnalyzer = new FakeLapAnalyzer(
        lastLap: new LapSummary { LapNumber = 21, LapTimeInMs = 90200, IsValid = true },
        bestLap: new LapSummary { LapNumber = 18, LapTimeInMs = 89900, IsValid = true });
    var fakeAi = new FakeAIAnalysisService(new AIAnalysisResult
    {
        IsSuccess = true,
        Summary = "Pace stable",
        TtsText = "Pace is stable."
    });

    var viewModel = CreateDashboardViewModel(fakeAnalyzer, fakeAi, aiEnabled: true);

    await viewModel.RunAiAnalysisIfNeededAsync();
    await viewModel.RunAiAnalysisIfNeededAsync();

    Assert.Equal(1, fakeAi.CallCount);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter DashboardAITriggerTests`
Expected: FAIL because dashboard AI trigger path does not exist yet

- [ ] **Step 3: Write minimal implementation**

```csharp
private int? _lastAnalyzedLapNumber;

private async Task RunAiAnalysisIfNeededAsync()
{
    var lastLap = _lapAnalyzer.CaptureLastLap();
    if (lastLap is null || _currentAiSettings is null || !_currentAiSettings.AiEnabled)
    {
        return;
    }

    if (_lastAnalyzedLapNumber == lastLap.LapNumber)
    {
        return;
    }

    _lastAnalyzedLapNumber = lastLap.LapNumber;
    var result = await _aiAnalysisService.AnalyzeAsync(BuildAiContext(lastLap), _currentAiSettings, _lifecycleCts.Token);
    EnqueueAiLog(result);
}
```

```xml
<CheckBox Content="启用 AI"
          IsChecked="{Binding AiEnabled}" />
<TextBox Text="{Binding AiBaseUrl, UpdateSourceTrigger=PropertyChanged}" />
<TextBox Text="{Binding AiModel, UpdateSourceTrigger=PropertyChanged}" />
<PasswordBox />
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter DashboardAITriggerTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add F1Telemetry.App F1Telemetry.Tests
git commit -m "feat: wire dashboard deepseek analysis"
```

### Task 6: Full verification and cleanup

**Files:**
- Modify: `F1Telemetry.Tests/F1Telemetry.Tests.csproj`
- Verify: `F1Telemetry.sln`
- Verify: `F1Telemetry.App/MainWindow.xaml`

- [ ] **Step 1: Run targeted AI tests**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --filter "PromptBuilderTests|AISettingsStoreTests|DeepSeekAnalysisServiceTests|DashboardAITriggerTests"`
Expected: all selected tests PASS

- [ ] **Step 2: Run full solution build**

Run: `dotnet build .\F1Telemetry.sln -c Debug --configfile .\NuGet.Config`
Expected: `0` warnings, `0` errors

- [ ] **Step 3: Run full test suite**

Run: `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-build`
Expected: all tests PASS

- [ ] **Step 4: Smoke-check dashboard startup**

Run: `Start-Process '.\.artifacts\bin\F1Telemetry.App\Debug\net8.0-windows\F1Telemetry.App.exe'`
Expected: app starts without immediate crash

- [ ] **Step 5: Commit**

```bash
git add F1Telemetry.AI F1Telemetry.App F1Telemetry.Tests
git commit -m "feat: complete deepseek milestone"
```
