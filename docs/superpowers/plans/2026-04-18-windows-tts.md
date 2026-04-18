# Windows TTS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a minimal Windows TTS pipeline that consumes race events and AI TTS text, persists TTS settings alongside AI settings, and shows recent AI / TTS logs in the WPF dashboard.

**Architecture:** Keep speech playback inside `F1Telemetry.TTS`, with `TtsQueue` owning priority, deduplication, cooldown, background consumption, and recent playback history. Replace the single-purpose AI settings store with one unified JSON settings store that preserves `ai` and `tts` blocks independently, then let `DashboardViewModel` only map event/AI outputs into `TtsMessage` instances and poll recent spoken records for display.

**Tech Stack:** .NET 8, WPF, MVVM, `System.Speech.Synthesis.SpeechSynthesizer`, `System.Text.Json`, xUnit

---

### Task 1: Add failing tests for unified settings storage

**Files:**
- Create: `F1Telemetry.AI/Interfaces/IAppSettingsStore.cs`
- Create: `F1Telemetry.AI/Models/AppSettingsDocument.cs`
- Create: `F1Telemetry.AI/Services/AppSettingsStore.cs`
- Modify: `F1Telemetry.Tests/AISettingsStoreTests.cs`

- [ ] **Step 1: Write the failing tests**
- [ ] **Step 2: Run the targeted settings tests and verify failure**
- [ ] **Step 3: Implement the minimal unified settings document + store**
- [ ] **Step 4: Run the targeted settings tests and verify pass**

### Task 2: Add failing tests for queue deduplication, cooldown, and priority

**Files:**
- Create: `F1Telemetry.TTS/Models/TtsMessage.cs`
- Create: `F1Telemetry.TTS/Models/TtsPlaybackRecord.cs`
- Create: `F1Telemetry.TTS/Models/TtsOptions.cs`
- Create: `F1Telemetry.TTS/TtsPriority.cs`
- Create: `F1Telemetry.TTS/Services/TtsQueue.cs`
- Test: `F1Telemetry.Tests/TtsQueueTests.cs`

- [ ] **Step 1: Write failing `TtsQueueTests` for queue deduplication**
- [ ] **Step 2: Run the targeted dedup test and verify failure**
- [ ] **Step 3: Add failing cooldown and priority tests**
- [ ] **Step 4: Run the targeted queue tests and verify failure**
- [ ] **Step 5: Implement the minimal queue, models, and recent-record read API**
- [ ] **Step 6: Run the targeted queue tests and verify pass**

### Task 3: Implement Windows speech service and queue integration

**Files:**
- Modify: `F1Telemetry.Core/Interfaces/ITtsService.cs`
- Modify: `F1Telemetry.TTS/F1Telemetry.TTS.csproj`
- Modify: `F1Telemetry.TTS/Services/WindowsTtsService.cs`

- [ ] **Step 1: Add a failing service-level test if a seam is needed**
- [ ] **Step 2: Implement the minimal `WindowsSpeechService` around `SpeechSynthesizer`**
- [ ] **Step 3: Keep playback async and exception-safe for queue consumption**
- [ ] **Step 4: Run the full TTS test set and verify pass**

### Task 4: Wire App settings, event/AI enqueueing, and unified AI / TTS log display

**Files:**
- Modify: `F1Telemetry.App/App.xaml.cs`
- Modify: `F1Telemetry.App/ViewModels/DashboardViewModel.cs`
- Modify: `F1Telemetry.App/MainWindow.xaml`
- Modify: `F1Telemetry.Tests/DeepSeekAnalysisServiceTests.cs`

- [ ] **Step 1: Update dashboard dependencies to use unified settings storage and TTS queue**
- [ ] **Step 2: Persist `tts` settings beside `ai` without overwriting the other block**
- [ ] **Step 3: Map `RaceEvent` and successful `AIAnalysisResult.TtsText` into `TtsMessage`**
- [ ] **Step 4: Show recent AI / TTS / System records in the existing log area**
- [ ] **Step 5: Run targeted app/config tests and verify pass**

### Task 5: Verify end-to-end behavior

**Files:**
- Modify: `F1Telemetry.Tests/F1Telemetry.Tests.csproj`

- [ ] **Step 1: Run `dotnet build .\F1Telemetry.sln -c Debug --configfile .\NuGet.Config`**
- [ ] **Step 2: Run `dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-build`**
- [ ] **Step 3: Review changed files for comments, scope, and milestone alignment**

