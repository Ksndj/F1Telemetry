# F1Telemetry V3 Full Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement V3 as a strategy analysis and corner-level driving analysis layer on top of the existing V2 history, review, EventBus, AI, TTS, and SQLite foundations.

**Architecture:** Keep V3 analysis models and deterministic calculations in `F1Telemetry.Analytics`, persistence in `F1Telemetry.Storage`, AI report prompt construction in `F1Telemetry.AI`, and WPF surfaces as thin ViewModel projections. V3 must preserve data-quality warnings and confidence values instead of producing precise-looking conclusions when samples or reference data are missing.

**Tech Stack:** .NET 10, WPF + MVVM, SQLite via `Microsoft.Data.Sqlite`, existing ScottPlot-based chart surfaces, existing DeepSeek-compatible AI integration.

---

## Execution Rules

- Work on branch `codex/v3-full-update`.
- Keep implementation split by milestone and commit each milestone separately.
- Do not change release version metadata or packaging scripts.
- Do not send raw high-frequency UDP, API keys, headers, JSONL logs, or local paths into AI prompts.
- Run module tests from simple to complex; do not run the full test suite.
- If a command produces no useful output for more than 10 minutes, stop it and switch to a smaller logged command.

## V3-M1: Track Segment Model

**Files:**
- Create Analytics track segmentation models and provider under `F1Telemetry.Analytics/Tracks/`.
- Test with `F1Telemetry.Tests/TrackSegmentMapProviderTests.cs`.

- [ ] Add `TrackSegmentType`, `TrackSegmentMapStatus`, `TrackSegment`, `TrackSegmentMap`, `TrackSegmentMapResult`, and `ITrackSegmentMapProvider`.
- [ ] Implement `StaticTrackSegmentMapProvider` with estimated maps for track IDs `0` Australia, `2` Shanghai, and `13` Suzuka.
- [ ] Return an unsupported result for unknown tracks without throwing.
- [ ] Test supported tracks, estimated map warnings, ordered non-overlapping segments, and unsupported fallback.
- [ ] Commit as `feat: add V3 track segment maps`.

## V3-M2: Corner Metrics Extraction

**Files:**
- Create corner models and extractor under `F1Telemetry.Analytics/Corners/`.
- Extend lap analysis enough to expose completed-lap samples.
- Add V3 lap sample persistence under `F1Telemetry.Storage/`.
- Test with `CornerMetricsExtractorTests`, `LapSampleRepositoryTests`, and storage persistence tests.

- [ ] Add shared confidence and data-quality models.
- [ ] Add `CornerSummary` and `CornerMetricsExtractor`.
- [ ] Compute entry speed, minimum speed, exit speed, max brake, throttle reapply distance, max steering, segment time, and optional reference-lap loss.
- [ ] Preserve completed-lap samples from `LapAnalyzer` and persist them through SQLite without blocking the UI thread.
- [ ] Add `lap_samples` table and repository with session/lap lookup.
- [ ] Test missing samples, low density, missing reference lap, estimated map warning, and stored sample roundtrip.
- [ ] Commit as `feat: add V3 corner metric extraction`.

## V3-M3: Corner Analysis View

**Files:**
- Add `CornerAnalysisViewModel` and `CornerAnalysisView` under `F1Telemetry.App/`.
- Wire shell navigation and app startup dependencies.
- Test with `CornerAnalysisViewModelTests`.

- [ ] Add a shell navigation item named `弯角分析`.
- [ ] Reuse historical session selection patterns and load stored lap samples lazily.
- [ ] Show corner list, time loss, speed/throttle/brake comparison, confidence, and warnings.
- [ ] Show explicit empty states for unsupported track, no selected session, no stored samples, and missing reference lap.
- [ ] Keep code-behind limited to initialization and layout sizing.
- [ ] Commit as `feat: add V3 corner analysis page`.

## V3-M4: Stint Strategy Timeline

**Files:**
- Add strategy analysis models and services under `F1Telemetry.Analytics/Strategy/`.
- Extend post-race review ViewModel projections.
- Test with `StintStrategyAnalyzerTests` and `PostRaceReviewViewModelTests`.

- [ ] Add `StintSummary` and `StrategyTimelineEntry`.
- [ ] Infer stints from tyre labels and pit flags while preserving raw metrics separately from adjusted metrics.
- [ ] Mark safety-car and red-flag influenced laps when events are available.
- [ ] Render stint and strategy timeline rows in the post-race review page.
- [ ] Commit as `feat: add V3 stint strategy timeline`.

## V3-M5: Undercut And Overcut Risk

**Files:**
- Add strategy advice models and analyzer under `F1Telemetry.Analytics/Strategy/`.
- Persist strategy advice under `F1Telemetry.Storage/`.
- Test with `StrategyRiskAnalyzerTests`.

- [ ] Add `StrategyAdvice`, `StrategyAdviceType`, and `StrategyRiskLevel`.
- [ ] Estimate undercut/overcut risk from available gap, tyre, pit-loss, lap-time, and traffic inputs.
- [ ] Output `RequiredData`, `MissingData`, confidence, risk level, summary, and reason.
- [ ] Use observe/insufficient-data advice when pit loss, gap, tyre, or reference pace is missing.
- [ ] Never output absolute commands.
- [ ] Commit as `feat: add V3 strategy risk advice`.

## V3-M6: Post-Race AI Engineer Report

**Files:**
- Add compressed report input and builder under `F1Telemetry.AI/Reports/`.
- Persist race engineer reports under `F1Telemetry.Storage/`.
- Show historical reports in WPF review surfaces.
- Test with `RaceEngineerReportBuilderTests`.

- [ ] Add report input models that contain only summaries, laps, stints, strategy advice, corner summaries, key events, and data-quality warnings.
- [ ] Build Markdown/JSON-ready report output that distinguishes data-supported findings from inferred suggestions.
- [ ] Ensure prompts and saved reports never include API keys, headers, raw UDP packets, or raw JSONL logs.
- [ ] Add storage table and repository for race engineer reports.
- [ ] Wire a read-only historical report section into post-race review.
- [ ] Commit as `feat: add V3 race engineer reports`.

## V3-M7: Advanced Visualization And Documentation

**Files:**
- Extend existing chart builders and WPF projections.
- Update `README.md` to describe V3 status and current capabilities without changing release version.

- [ ] Add corner loss heat table or chart using `CornerSummary` values.
- [ ] Add strategy timeline visualization from `StrategyTimelineEntry`.
- [ ] Add ERS, tyre, and fuel distribution panels where stored data exists.
- [ ] Keep map/heatmap features behind data-availability empty states when segment or coordinate data is insufficient.
- [ ] Update README current features and V3 progress notes.
- [ ] Commit as `docs: update V3 implementation status`.

## Verification And PR

- [ ] Run module tests in this order: `TrackSegmentMapProviderTests`, `CornerMetricsExtractorTests`, `LapSampleRepositoryTests`, `StoragePersistenceServiceTests`, `StintStrategyAnalyzerTests`, `StrategyRiskAnalyzerTests`, `RaceEngineerReportBuilderTests`, `CornerAnalysisViewModelTests`, `PostRaceReviewViewModelTests`.
- [ ] Run `dotnet build .\F1Telemetry.sln -m:1`.
- [ ] Verify `git status --short` only contains intended V3 files before staging.
- [ ] Push `codex/v3-full-update` to `origin`.
- [ ] Create a Draft PR summarizing V3-M1 through V3-M7, tests run, and residual risks.
