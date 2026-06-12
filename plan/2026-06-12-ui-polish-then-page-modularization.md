# F1Telemetry UI Polish Then Page Modularization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` for parallel page audits when the implementation scope is split across multiple pages, or `superpowers:executing-plans` for a single-page milestone. Track progress with the checkbox items below and keep each milestone independently buildable.

**Goal:** First stabilize and polish every page UI in small page-level milestones, then start the page-independent module plan only after UI behavior is stable and tested.

**Architecture:** Keep the current WPF + MVVM structure. Page polish stays in `F1Telemetry.App/Views`, shared keyed resources stay in `F1Telemetry.App/Styles`, and UI behavior helpers stay in `F1Telemetry.App/Behaviors` only when XAML alone cannot express the behavior safely. Page modularization later extracts page ViewModels and local resources gradually without moving business logic across layers.

**Tech Stack:** WPF/XAML, MVVM, .NET, xUnit UI/XAML static tests, existing `F1Telemetry.App`, `F1Telemetry.Core`, `F1Telemetry.Analytics`, `F1Telemetry.Storage`, `F1Telemetry.AI`, `F1Telemetry.TTS`, and `F1Telemetry.Udp` projects.

## Scope Boundaries

- [ ] Do not continue the previous global UI rewrite.
- [ ] Do not add broad implicit styles for all `Button`, `ComboBox`, `TextBox`, `CheckBox`, `Slider`, or `DataGrid`.
- [ ] Do not change UDP parser, database schema, AI/TTS/RaceAssistant business logic, or ViewModel data flow during UI polish.
- [ ] Do not use `Viewbox` around whole pages.
- [ ] Do not increase window `MinWidth` as a workaround for layout problems.
- [ ] Keep the top UDP port group as one non-wrapping unit.
- [ ] Keep read-only fields `OneWay` or rendered as `TextBlock`.
- [ ] Use one page-level milestone per commit so regressions can be reverted narrowly.

## Current Page Map

- `F1Telemetry.App/Views/OverviewView.xaml` - real-time overview.
- `F1Telemetry.App/Views/ChartsView.xaml` - analysis broadcast.
- `F1Telemetry.App/Views/LapHistoryView.xaml` - lap history.
- `F1Telemetry.App/Views/PostRaceReviewView.xaml` - post-race review.
- `F1Telemetry.App/Views/SessionComparisonView.xaml` - session comparison.
- `F1Telemetry.App/Views/CornerAnalysisView.xaml` - corner analysis.
- `F1Telemetry.App/Views/OpponentsView.xaml` - opponents.
- `F1Telemetry.App/Views/LogsView.xaml` - event logs.
- `F1Telemetry.App/Views/AiTtsView.xaml` - AI/TTS.
- `F1Telemetry.App/Views/SettingsView.xaml` - settings.
- `F1Telemetry.App/Views/Shell/NavigationTemplateSelector.cs` - page routing.
- `F1Telemetry.App/Styles/SharedStyles.xaml`, `ThemeColors.xaml`, `ScrollBarStyles.xaml`, `ShellStyles.xaml` - shared UI resources.

## Phase 0 - Baseline Audit

- [ ] Capture current branch, HEAD, and dirty files.
- [ ] List all page XAML files that still show raw object names, default white controls, long untrimmed paths, or fragile scroll/dropdown behavior.
- [ ] Identify existing tests that guard each page before editing.
- [ ] Add missing static UI/XAML tests before changing a page when the bug can be expressed as a text/template contract.
- [ ] Save noisy command output under `.logs/` and report only summaries.

Baseline verification commands:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "MainWindow|ComboBox|Settings|AiBroadcast|PostRaceReview|SessionComparison|LapHistory|CornerAnalysis|Logs|Opponents" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
dotnet build F1Telemetry.sln --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

## Phase 1 - Gradual Page UI Polish

Work page by page. Each page milestone must finish tests, build, commit, and push before the next page begins.

### 1. Analysis Broadcast

- [ ] Polish `F1Telemetry.App/Views/ChartsView.xaml` operation area controls.
- [ ] Ensure mode `ComboBox` displays user-facing text instead of raw enum or type names.
- [ ] Prevent dropdown wheel events from scrolling the page while the pointer is inside the dropdown.
- [ ] Keep AI-disabled generate button behavior and report clearing behavior covered by tests.
- [ ] Verify chart panes still show empty states when data is insufficient.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "AiAnalysis|Dashboard|ComboBox|Charts" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 2. AI / TTS

- [ ] Polish `F1Telemetry.App/Views/AiTtsView.xaml` only after Analysis Broadcast is stable.
- [ ] Keep voice, microphone, talk mode, tire inventory, and logs usable in small windows.
- [ ] Keep `ComboBox` display contracts: no `ViewModel.ToString()` type names.
- [ ] Keep dropdown list scrolling usable for long voice/device lists.
- [ ] Keep `+/-` stepper buttons readable and centered.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "AiTts|ComboBox|Settings" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 3. Settings

- [ ] Polish `F1Telemetry.App/Views/SettingsView.xaml` in small sections: logs, UDP raw log, voice AI, updates.
- [ ] Truncate long paths with tooltip full text.
- [ ] Keep read-only fields `OneWay` or `TextBlock`.
- [ ] Keep microphone and talk mode dropdowns scrollable and selectable.
- [ ] Keep editable settings `TwoWay` with existing `UpdateSourceTrigger`.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Settings|ComboBox|MainWindow" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 4. Lap History

- [ ] Polish `F1Telemetry.App/Views/LapHistoryView.xaml` after core dropdown behavior is stable.
- [ ] Keep session and lap bindings unchanged.
- [ ] Ensure page-level vertical scrolling and table-level horizontal scrolling are both accessible.
- [ ] Keep delete, refresh, and pagination buttons explicit page styles instead of broad global styles.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "LapHistory|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 5. Post-Race Review

- [ ] Polish `F1Telemetry.App/Views/PostRaceReviewView.xaml`.
- [ ] Preserve chart bindings and empty chart states.
- [ ] Verify chart axes do not show unsafe negative ranges when data is sparse.
- [ ] Keep export and refresh controls reachable in small windows.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "PostRaceReview|Chart|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 6. Session Comparison

- [ ] Polish `F1Telemetry.App/Views/SessionComparisonView.xaml`.
- [ ] Ensure track filter displays `DisplayName`, not `SessionComparisonTrackFilterViewModel`.
- [ ] Keep chart bindings unchanged.
- [ ] Verify chart axes and empty states.
- [ ] Keep selected sessions readable and removable without layout overlap.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "SessionComparison|ComboBox|Chart" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 7. Corner Analysis

- [ ] Polish `F1Telemetry.App/Views/CornerAnalysisView.xaml` only after shared dropdown behavior is stable.
- [ ] Keep session, lap, and reference selectors usable with long lists.
- [ ] Preserve chart and corner analysis bindings.
- [ ] Verify small-window access with page scrolling.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "CornerAnalysis|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 8. Opponents

- [ ] Polish `F1Telemetry.App/Views/OpponentsView.xaml`.
- [ ] Keep empty states user-facing: waiting for opponent data or telemetry limited.
- [ ] Keep internal codes in tooltips or logs, not primary UI text.
- [ ] Use card/list polish locally without introducing broad global templates.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Opponents" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 9. Event Logs

- [ ] Polish `F1Telemetry.App/Views/LogsView.xaml`.
- [ ] Keep time, category, and summary visible.
- [ ] Trim long text and expose the full text through tooltip.
- [ ] Preserve `LogEntries` binding.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "LogsView|Logs|ComboBox" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

### 10. Overview Regression Pass

- [ ] Treat `F1Telemetry.App/Views/OverviewView.xaml` as the style baseline, not a rewrite target.
- [ ] Only fix concrete regressions found during other page work.
- [ ] Verify top status and UDP port group remain non-wrapping.

Recommended tests:

```powershell
dotnet test F1Telemetry.Tests/F1Telemetry.Tests.csproj --filter "Overview|MainWindow|UdpPort" --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal
```

## Page Milestone Checklist

Use this checklist for every page polish commit.

- [ ] Inspect the page XAML, ViewModel bindings, and existing tests.
- [ ] Add or update the narrowest UI/XAML test first when practical.
- [ ] Make local XAML/style changes only for the current page.
- [ ] Avoid changing ViewModel data flow.
- [ ] Check small-window behavior: page scrolls vertically, tables/lists scroll internally when needed.
- [ ] Check dropdown behavior: opens, scrolls internally, can select, does not show object names.
- [ ] Check disabled controls remain readable.
- [ ] Run the page-specific filtered test command.
- [ ] Run solution build.
- [ ] Manually verify the page in the app when the change affects runtime interaction.
- [ ] Commit and push only files from the current milestone.

## Shared Style Rules

- [ ] Keep shared resources keyed and opt-in.
- [ ] Do not introduce global implicit control templates unless a separate stabilization plan explicitly approves it.
- [ ] Add a shared keyed style only after at least two pages need the same stable pattern.
- [ ] Prefer page-local layout fixes over shared template changes.
- [ ] Keep `ComboBox` item text trimming, tooltip full text, and `MaxDropDownHeight` as shared contracts when safe.
- [ ] Do not attach page-level scroll listeners that close dropdowns while the pointer or mouse capture is inside the dropdown popup.

## Phase 2 - Readiness Gate Before Modularization

Do not start page module extraction until all gate items are true.

- [ ] Every page listed in Phase 1 has a passing targeted test command.
- [ ] `dotnet build F1Telemetry.sln --no-restore -p:UseSharedCompilation=false -m:1 -v:minimal` passes.
- [ ] Manual smoke checks pass for settings, AI/TTS, analysis broadcast, session comparison, corner analysis, lap history, and post-race review.
- [ ] No known blocking ComboBox regressions remain.
- [ ] No page requires a large global style/template rewrite to remain usable.
- [ ] Current UI limitations are documented in a plan or issue.

## Phase 3 - Page-Independent Module Plan

Start modularization only after Phase 2 passes. The first modularization goal is reducing change difficulty, not creating many projects.

### Module Definition

A page module should contain:

- The page view XAML and code-behind.
- A page-specific ViewModel when the page currently depends on a large shared ViewModel surface.
- Page-local converters, templates, and keyed resources when they are not shared by other pages.
- Focused tests for that page's bindings and UI contracts.

### Recommended Extraction Order

- [ ] Extract Analysis Broadcast page state from broad dashboard responsibilities into a dedicated page ViewModel while preserving existing public behavior.
- [ ] Extract AI/TTS page settings and display state into a dedicated page ViewModel or coordinator facade.
- [ ] Extract Settings page state into a dedicated settings ViewModel where it reduces binding risk.
- [ ] Extract Lap History page browser state behind a page-level interface or facade if the current surface remains too wide.
- [ ] Keep Post-Race Review, Session Comparison, and Corner Analysis on their existing specialized ViewModels unless a real duplication or ownership issue appears.

### Modularization Boundaries

- [ ] Keep business services in their current domain projects.
- [ ] Keep UDP parsing in `F1Telemetry.Udp`.
- [ ] Keep storage schema and migration code unchanged unless a separate data task requests it.
- [ ] Keep AI/TTS/RaceAssistant logic in their existing services.
- [ ] Keep the app shell responsible only for navigation, global status, and page composition.
- [ ] Do not create per-page projects until page ViewModels and tests are stable enough to justify that cost.

## Expected Benefits And Costs

- Page-level UI polish first reduces the chance that module extraction preserves a broken UI state.
- One page per milestone makes regressions easier to isolate and revert.
- Dedicated page modules later should reduce change difficulty by shrinking binding surfaces and test scope.
- Code complexity can increase if modules are created too early, especially if shared state is copied instead of owned clearly.
- The safest path is to extract ownership only where a page has a repeated, proven maintenance problem.

## Final Verification Template

For each implementation milestone, report:

- Modified files.
- Page or module polished.
- Bindings intentionally preserved.
- Tests/build results.
- Manual verification result.
- Current branch.
- Commit hash.
- Push status.
- Remaining risks or known UI limitations.
