# Milestone 10 Charts Implementation Plan

> 2026-04-30 更新：驾驶中查看实时图表不再作为当前产品方向。本计划保留为历史参考，不再作为实施入口；速度、输入、燃油和胎磨趋势应优先压缩进 AI 分析播报，并通过 TTS 输出关键结论。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the middle dashboard placeholders with four real charts backed by existing lap analytics and lap-history data, while fixing the known chart-module correctness risks and the most important refresh-performance hotspots.

**Architecture:** Keep all sampling and aggregation inside `Analytics`, expose only immutable read-only chart inputs, and build App-layer chart models through dedicated adapters. Use a lightweight WPF chart host for ScottPlot so rendering stays in the view layer while all chart data shaping stays outside `DashboardViewModel`, and fix the known update/empty-state issues without changing the module split or adding new dependencies. Four-wheel wear needed by charts stays inside `Analytics` by extending `LapAnalyzer` and `LapBuilder`, so this milestone does not spill into `Core`.

**Tech Stack:** .NET 8 WPF, existing MVVM structure, `ScottPlot.WPF` for chart rendering, xUnit for tests.

---

## Scope Guardrails

- Chart-related code changes stay inside:
  - `F1Telemetry.Analytics`
  - `F1Telemetry.App\Charts`
  - `F1Telemetry.App\ViewModels`
  - `F1Telemetry.App\Views\Controls`
  - `F1Telemetry.App\MainWindow.xaml`
- The only allowed supporting file outside those folders is `F1Telemetry.App.csproj`, strictly for the ScottPlot package reference.
- No chart-driven refactors are allowed in `Core`, `AI`, `TTS`, `Storage`, or unrelated UI surfaces.

## File Map

**Modify**
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\F1Telemetry.App.csproj`
  Add the ScottPlot WPF package, falling back to another stable `5.x` version only if `5.0.36` cannot restore.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Interfaces\ILapAnalyzer.cs`
  Expose a read-only current-lap sample capture API.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapSample.cs`
  Carry four-wheel wear alongside existing average wear.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapSummary.cs`
  Carry four-wheel wear delta summary fields for trend charts.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapBuilder.cs`
  Close laps with per-wheel wear deltas.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapAnalyzer.cs`
  Return current-lap samples as immutable snapshots rather than leaking builder-owned collections, and capture player-wheel wear from parsed `CarDamagePacket` inside analytics.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\ViewModels\DashboardViewModel.cs`
  Own four chart-specific child view models and throttle chart refresh.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\MainWindow.xaml`
  Replace placeholder cards with four real chart panels while preserving layout.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\LapAnalyzerTests.cs`
  Cover current-lap sample capture, immutable snapshots, and analytics-owned wheel-wear propagation.

**Create**
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\ChartPointModel.cs`
  Small immutable chart point DTO for UI consumption.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\ChartSeriesModel.cs`
  A named series with unit-ready display metadata.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\CurrentLapChartBuilder.cs`
  Convert current-lap `LapSample` data into down-sampled speed and throttle/brake series.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\TrendChartBuilder.cs`
  Convert lap summaries into fuel and tyre wear trend series.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\ViewModels\ChartPanelViewModel.cs`
  Bindable chart panel state with explicit property-change notifications for chart data and empty-state transitions.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Views\Controls\TelemetryChartControl.xaml`
  View-only chart host.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Views\Controls\TelemetryChartControl.xaml.cs`
  ScottPlot rendering wrapper driven entirely by bindable series models.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\CurrentLapChartBuilderTests.cs`
  Validate speed and throttle/brake chart adaptation.
- `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\TrendChartBuilderTests.cs`
  Validate fuel litres and four-wheel wear trend adaptation.

## Task 1: Expose chart-ready lap data from Analytics

**Files:**
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Interfaces\ILapAnalyzer.cs`
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapSample.cs`
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapSummary.cs`
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapBuilder.cs`
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Analytics\Laps\LapAnalyzer.cs`
- Test: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\LapAnalyzerTests.cs`

- [ ] **Step 1: Write the failing analytics tests**

```csharp
[Fact]
public void CaptureCurrentLapSamples_ReturnsCurrentBuilderSamplesNewestSnapshotSafe()
{
    var analyzer = new LapAnalyzer();
    var state = CreatePlayerState(
        lapNumber: 4,
        lapDistance: 150f,
        speedKph: 298d,
        throttle: 0.92d,
        brake: 0.05d,
        tyreWearPerWheel: new WheelSet<float>(24f, 25f, 23f, 22f));

    analyzer.Observe(CreateLapDataParsedPacket(frameIdentifier: 10, lapNumber: 4), state);

    var samples = analyzer.CaptureCurrentLapSamples();

    Assert.Single(samples);
    Assert.Equal(150f, samples[0].LapDistance);
    Assert.Equal(298d, samples[0].SpeedKph);
    Assert.Equal(24f, samples[0].TyreWearPerWheel!.RearLeft);
}

[Fact]
public void CaptureCurrentLapSamples_ReturnsIndependentSnapshot()
{
    var analyzer = new LapAnalyzer();
    analyzer.Observe(CreateLapDataParsedPacket(frameIdentifier: 10, lapNumber: 4), CreatePlayerState(lapNumber: 4, lapDistance: 100f));

    var first = analyzer.CaptureCurrentLapSamples();
    analyzer.Observe(CreateLapDataParsedPacket(frameIdentifier: 11, lapNumber: 4), CreatePlayerState(lapNumber: 4, lapDistance: 120f));
    var second = analyzer.CaptureCurrentLapSamples();

    Assert.Single(first);
    Assert.Equal(100f, first[0].LapDistance);
    Assert.Equal(2, second.Count);
}

[Fact]
public void Observe_WithPlayerCarDamagePacket_CapturesPerWheelTyreWearForCurrentLapSample()
{
    var analyzer = new LapAnalyzer();
    analyzer.Observe(CreateLapDataParsedPacket(frameIdentifier: 10, lapNumber: 4), CreatePlayerState(lapNumber: 4, lapDistance: 90f));
    analyzer.Observe(
        CreateCarDamageParsedPacket(
            frameIdentifier: 11,
            playerCarIndex: 0,
            tyreWear: new WheelSet<float>(11f, 12f, 13f, 14f)),
        CreatePlayerState(lapNumber: 4, lapDistance: 110f));

    var samples = analyzer.CaptureCurrentLapSamples();
    var latest = samples[^1];

    Assert.NotNull(latest.TyreWearPerWheel);
    Assert.Equal(14f, latest.TyreWearPerWheel!.FrontRight);
}
```

- [ ] **Step 2: Run the targeted tests and verify they fail**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LapAnalyzerTests|FullyQualifiedName~StateAggregatorTests"
```

Expected: FAIL because `CaptureCurrentLapSamples`, immutable snapshot behavior, and analytics-owned wheel-wear capture do not exist yet.

- [ ] **Step 3: Add the minimum analytics surface**

```csharp
public interface ILapAnalyzer
{
    IReadOnlyList<LapSample> CaptureCurrentLapSamples();
}

public sealed record LapSample
{
    public WheelSet<float>? TyreWearPerWheel { get; init; }
}

public sealed record LapSummary
{
    public WheelSet<float>? TyreWearDeltaPerWheel { get; init; }
}
```

- [ ] **Step 4: Implement wear propagation and current-lap capture**

```csharp
public IReadOnlyList<LapSample> CaptureCurrentLapSamples()
{
    lock (_syncRoot)
    {
        return _currentLapBuilder is null
            ? Array.Empty<LapSample>()
            : _currentLapBuilder.CaptureSamples().ToArray();
    }
}

private WheelSet<float>? _latestPlayerTyreWearPerWheel;

public void Observe(ParsedPacket parsedPacket, SessionState sessionState)
{
    if (parsedPacket.Packet is CarDamagePacket damagePacket &&
        parsedPacket.Header.PlayerCarIndex < damagePacket.Cars.Length)
    {
        _latestPlayerTyreWearPerWheel = damagePacket.Cars[parsedPacket.Header.PlayerCarIndex].TyreWear;
    }

    // Existing lap-sample creation keeps running, but now copies _latestPlayerTyreWearPerWheel into each sample.
}
```

- [ ] **Step 5: Close laps with per-wheel wear deltas**

```csharp
return new LapSummary
{
    FuelUsedLitres = fuelUsedLitres,
    TyreWearDelta = averageWearDelta,
    TyreWearDeltaPerWheel = start.TyreWearPerWheel is null || end.TyreWearPerWheel is null
        ? null
        : new WheelSet<float>(
            end.TyreWearPerWheel.RearLeft - start.TyreWearPerWheel.RearLeft,
            end.TyreWearPerWheel.RearRight - start.TyreWearPerWheel.RearRight,
            end.TyreWearPerWheel.FrontLeft - start.TyreWearPerWheel.FrontLeft,
            end.TyreWearPerWheel.FrontRight - start.TyreWearPerWheel.FrontRight)
};
```

- [ ] **Step 6: Re-run the targeted tests**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LapAnalyzerTests"
```

Expected: PASS for the newly added analytics assertions.

- [ ] **Step 7: Commit the analytics data export changes**

```bash
git add F1Telemetry.Analytics F1Telemetry.Tests
git commit -m "feat: expose chart-ready lap data"
```

## Task 2: Build chart data adapters outside the dashboard VM

**Files:**
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\ChartPointModel.cs`
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\ChartSeriesModel.cs`
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\CurrentLapChartBuilder.cs`
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Charts\TrendChartBuilder.cs`
- Test: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\CurrentLapChartBuilderTests.cs`
- Test: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\TrendChartBuilderTests.cs`

- [ ] **Step 1: Write failing chart builder tests**

```csharp
[Fact]
public void BuildSpeedSeries_WithCurrentLapSamples_UsesLapDistanceAndSpeed()
{
    var builder = new CurrentLapChartBuilder();
    var samples = new[]
    {
        new LapSample { LapDistance = 10f, SpeedKph = 180d },
        new LapSample { LapDistance = 20f, SpeedKph = 205d }
    };

    var panel = builder.BuildSpeedPanel(samples);

    Assert.False(panel.IsEmpty);
    Assert.Equal("当前圈速度", panel.Title);
    Assert.Equal("km/h", panel.YAxisLabel);
    Assert.Equal(2, panel.Series[0].Points.Count);
}

[Fact]
public void BuildFuelTrend_WithLapHistory_UsesFuelUsedLitres()
{
    var builder = new TrendChartBuilder();
    var laps = new[]
    {
        new LapSummary { LapNumber = 5, FuelUsedLitres = 1.85f },
        new LapSummary { LapNumber = 6, FuelUsedLitres = 1.93f }
    };

    var panel = builder.BuildFuelTrendPanel(laps);

    Assert.Equal("L", panel.YAxisLabel);
    Assert.Equal(1.85d, panel.Series[0].Points[0].Y);
    Assert.DoesNotContain("kg", panel.Title, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void BuildTyreWearTrend_WithNullWheelDelta_SkipsIncompleteLapWithoutThrowing()
{
    var builder = new TrendChartBuilder();
    var laps = new[]
    {
        new LapSummary { LapNumber = 5, TyreWearDeltaPerWheel = null },
        new LapSummary
        {
            LapNumber = 6,
            TyreWearDeltaPerWheel = new WheelSet<float>(1.1f, 1.3f, 1.4f, 1.2f)
        }
    };

    var panel = builder.BuildTyreWearTrendPanel(laps);

    Assert.False(panel.IsEmpty);
    Assert.All(panel.Series, series => Assert.Single(series.Points));
    Assert.All(panel.Series, series => Assert.Equal(6d, series.Points[0].X));
}
```

- [ ] **Step 2: Run the new chart builder tests and verify they fail**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CurrentLapChartBuilderTests|FullyQualifiedName~TrendChartBuilderTests"
```

Expected: FAIL because the chart models and builders do not exist yet.

- [ ] **Step 3: Add the immutable chart DTOs**

```csharp
public sealed record ChartPointModel(double X, double Y);

public sealed record ChartSeriesModel(
    string Name,
    string StrokeKey,
    IReadOnlyList<ChartPointModel> Points);
```

- [ ] **Step 4: Implement current-lap chart adaptation with down-sampling**

```csharp
public sealed class CurrentLapChartBuilder
{
    public ChartPanelViewModel BuildSpeedPanel(IReadOnlyList<LapSample> samples) { /* ... */ }

    public ChartPanelViewModel BuildThrottleBrakePanel(IReadOnlyList<LapSample> samples) { /* ... */ }

    private static IReadOnlyList<LapSample> DownSample(IReadOnlyList<LapSample> samples, int maxPoints)
    {
        if (samples.Count <= maxPoints) return samples;
        return DownSampleWithPeakPreservation(samples, maxPoints);
    }

    private static IReadOnlyList<LapSample> DownSampleWithPeakPreservation(IReadOnlyList<LapSample> samples, int maxPoints)
    {
        // Always keep the first and last point.
        // Split the interior points into buckets.
        // For each bucket, keep up to two points in original order:
        //   the local minimum and the local maximum for the chart's active metric.
        // This preserves speed and brake spikes better than stride-based skipping.
    }
}
```

- [ ] **Step 5: Implement multi-lap fuel and four-wheel wear trends**

```csharp
public sealed class TrendChartBuilder
{
    public ChartPanelViewModel BuildFuelTrendPanel(IReadOnlyList<LapSummary> laps) { /* X = lap number, Y = FuelUsedLitres */ }

    public ChartPanelViewModel BuildTyreWearTrendPanel(IReadOnlyList<LapSummary> laps)
    {
        // Ignore laps with null TyreWearDeltaPerWheel.
        // If every lap is incomplete, return an empty panel with "暂无历史圈数据".
    }
}
```

- [ ] **Step 6: Re-run the chart builder tests**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CurrentLapChartBuilderTests|FullyQualifiedName~TrendChartBuilderTests"
```

Expected: PASS, including the litres-only assertions.

- [ ] **Step 7: Commit the chart adapter layer**

```bash
git add F1Telemetry.App\Charts F1Telemetry.Tests
git commit -m "feat: add chart data adapters"
```

## Task 3: Add a view-only ScottPlot host and bindable chart panels

**Files:**
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\F1Telemetry.App.csproj`
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\ViewModels\ChartPanelViewModel.cs`
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Views\Controls\TelemetryChartControl.xaml`
- Create: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\Views\Controls\TelemetryChartControl.xaml.cs`

- [ ] **Step 1: Add a focused package-level failing build change**

```xml
<ItemGroup>
  <PackageReference Include="ScottPlot.WPF" Version="5.0.36" />
</ItemGroup>
```

If `5.0.36` fails to restore, pin the nearest restorable stable `5.x` release. Do not switch chart libraries and do not change the architecture because of package version friction.

- [ ] **Step 2: Create a bindable panel VM with explicit empty-state support**

```csharp
public sealed class ChartPanelViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _xAxisLabel = string.Empty;
    private string _yAxisLabel = string.Empty;
    private string _emptyMessage = string.Empty;
    private bool _isEmpty;
    private IReadOnlyList<ChartSeriesModel> _series = Array.Empty<ChartSeriesModel>();

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string XAxisLabel { get => _xAxisLabel; private set => SetProperty(ref _xAxisLabel, value); }
    public string YAxisLabel { get => _yAxisLabel; private set => SetProperty(ref _yAxisLabel, value); }
    public string EmptyMessage { get => _emptyMessage; private set => SetProperty(ref _emptyMessage, value); }
    public bool IsEmpty { get => _isEmpty; private set => SetProperty(ref _isEmpty, value); }
    public IReadOnlyList<ChartSeriesModel> Series { get => _series; private set => SetProperty(ref _series, value); }

    public void UpdateFrom(ChartPanelViewModel source)
    {
        Title = source.Title;
        XAxisLabel = source.XAxisLabel;
        YAxisLabel = source.YAxisLabel;
        EmptyMessage = source.EmptyMessage;
        IsEmpty = source.IsEmpty;
        Series = source.Series;
    }
}
```

- [ ] **Step 3: Implement the view-only chart control**

```xml
<UserControl x:Class="F1Telemetry.App.Views.Controls.TelemetryChartControl"
             xmlns:scottPlot="clr-namespace:ScottPlot.WPF;assembly=ScottPlot.WPF">
  <Grid>
    <scottPlot:WpfPlot x:Name="PlotHost" Visibility="{Binding IsEmpty, Converter={StaticResource InverseBooleanToVisibilityConverter}}" />
    <Border Visibility="{Binding IsEmpty, Converter={StaticResource BooleanToVisibilityConverter}}">
      <TextBlock Text="{Binding EmptyMessage}" />
    </Border>
  </Grid>
</UserControl>
```

The control must refresh when any of these change:
- `DataContext`
- `ChartPanelViewModel.Series`
- `ChartPanelViewModel.IsEmpty`

The implementation should subscribe and unsubscribe on `DataContextChanged`, then re-render on the relevant `PropertyChanged` notifications.

- [ ] **Step 4: Keep rendering logic in the control, not in the VM**

```csharp
private void RenderSeries()
{
    EnsureScatterSeries();
    UpdateScatterData();

    PlotHost.Plot.Axes.Bottom.Label.Text = XAxisLabel;
    PlotHost.Plot.Axes.Left.Label.Text = YAxisLabel;
    PlotHost.Refresh();
}
```

If ScottPlot 5 supports direct series data replacement cleanly, use that instead of `Plot.Clear()`. Only fall back to full rebuild when series counts change or the control is reinitialized.

- [ ] **Step 5: Build the App project to verify the chart host compiles**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe build .\F1Telemetry.App\F1Telemetry.App.csproj -c Debug --no-restore
```

Expected: PASS with the chart host and package reference in place.

- [ ] **Step 6: Commit the chart host scaffolding**

```bash
git add F1Telemetry.App
git commit -m "feat: add chart host controls"
```

## Task 4: Replace placeholder charts in the dashboard

**Files:**
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\ViewModels\DashboardViewModel.cs`
- Modify: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.App\MainWindow.xaml`

- [ ] **Step 1: Add dashboard-facing chart state without growing giant methods further**

```csharp
public ChartPanelViewModel CurrentLapSpeedChart { get; }
public ChartPanelViewModel CurrentLapThrottleBrakeChart { get; }
public ChartPanelViewModel FuelTrendChart { get; }
public ChartPanelViewModel TyreWearTrendChart { get; }

private readonly CurrentLapChartBuilder _currentLapChartBuilder;
private readonly TrendChartBuilder _trendChartBuilder;
private DateTimeOffset _lastCurrentLapChartRefreshAt;
private int _lastTrendLapNumber;
```

- [ ] **Step 2: Refresh current-lap charts on a throttle**

```csharp
private void RefreshCurrentLapCharts(DateTimeOffset now)
{
    if (now - _lastCurrentLapChartRefreshAt < TimeSpan.FromMilliseconds(250))
    {
        return;
    }

    var samples = _lapAnalyzer.CaptureCurrentLapSamples();
    CurrentLapSpeedChart.UpdateFrom(_currentLapChartBuilder.BuildSpeedPanel(samples));
    CurrentLapThrottleBrakeChart.UpdateFrom(_currentLapChartBuilder.BuildThrottleBrakePanel(samples));
    _lastCurrentLapChartRefreshAt = now;
}
```

- [ ] **Step 3: Refresh multi-lap trends only when a new lap closes**

```csharp
private void RefreshTrendChartsIfNeeded()
{
    var lastLap = _lapAnalyzer.CaptureLastLap();
    if (lastLap is null || lastLap.LapNumber == _lastTrendLapNumber)
    {
        return;
    }

    var recentLaps = _lapAnalyzer.CaptureRecentLaps(12);
    FuelTrendChart.UpdateFrom(_trendChartBuilder.BuildFuelTrendPanel(recentLaps));
    TyreWearTrendChart.UpdateFrom(_trendChartBuilder.BuildTyreWearTrendPanel(recentLaps));
    _lastTrendLapNumber = lastLap.LapNumber;
}
```

- [ ] **Step 3.1: Keep dashboard refresh logic thin**

```csharp
private void RefreshCharts(DateTimeOffset now)
{
    RefreshCurrentLapCharts(now);
    RefreshTrendChartsIfNeeded();
}
```

This step is required to keep chart logic out of the existing giant dashboard methods instead of spreading chart calculations across unrelated refresh paths.

- [ ] **Step 4: Replace the placeholder XAML with four real chart panels**

```xml
<Grid Grid.Row="1" Margin="0,18,0,0">
  <Grid.RowDefinitions>
    <RowDefinition Height="*" />
    <RowDefinition Height="*" />
  </Grid.RowDefinitions>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
  </Grid.ColumnDefinitions>

  <views:TelemetryChartControl Grid.Row="0" Grid.Column="0" DataContext="{Binding CurrentLapSpeedChart}" />
  <views:TelemetryChartControl Grid.Row="0" Grid.Column="1" DataContext="{Binding CurrentLapThrottleBrakeChart}" />
  <views:TelemetryChartControl Grid.Row="1" Grid.Column="0" DataContext="{Binding FuelTrendChart}" />
  <views:TelemetryChartControl Grid.Row="1" Grid.Column="1" DataContext="{Binding TyreWearTrendChart}" />
</Grid>
```

- [ ] **Step 5: Smoke-build the full solution**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe build .\F1Telemetry.sln -c Debug --no-restore
```

Expected: PASS with the dashboard still compiling and the existing side panels unaffected.

- [ ] **Step 6: Commit the dashboard chart integration**

```bash
git add F1Telemetry.App
git commit -m "feat: wire live dashboard charts"
```

## Task 5: Verify chart data correctness and milestone completion

**Files:**
- Modify if needed: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\CurrentLapChartBuilderTests.cs`
- Modify if needed: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\TrendChartBuilderTests.cs`
- Modify if needed: `C:\Users\10670\OneDrive\文档\F1遥测\F1Telemetry.Tests\LapAnalyzerTests.cs`

- [ ] **Step 1: Add explicit empty-state coverage**

```csharp
[Fact]
public void BuildThrottleBrakePanel_WithoutSamples_ReturnsEmptyPanel()
{
    var builder = new CurrentLapChartBuilder();

    var panel = builder.BuildThrottleBrakePanel(Array.Empty<LapSample>());

    Assert.True(panel.IsEmpty);
    Assert.Equal("等待当前圈采样", panel.EmptyMessage);
}

[Fact]
public void BuildTyreWearTrendPanel_WithoutLapHistory_ReturnsEmptyPanel()
{
    var builder = new TrendChartBuilder();

    var panel = builder.BuildTyreWearTrendPanel(Array.Empty<LapSummary>());

    Assert.True(panel.IsEmpty);
    Assert.Equal("暂无历史圈数据", panel.EmptyMessage);
}

[Fact]
public void BuildTyreWearTrendPanel_WithOnlyNullWheelDeltas_ReturnsEmptyPanel()
{
    var builder = new TrendChartBuilder();

    var panel = builder.BuildTyreWearTrendPanel(new[]
    {
        new LapSummary { LapNumber = 8, TyreWearDeltaPerWheel = null }
    });

    Assert.True(panel.IsEmpty);
    Assert.Equal("暂无历史圈数据", panel.EmptyMessage);
}
```

- [ ] **Step 2: Add the litres-only trend assertion**

```csharp
[Fact]
public void BuildFuelTrendPanel_UsesLitresNamingOnly()
{
    var builder = new TrendChartBuilder();
    var panel = builder.BuildFuelTrendPanel(new[]
    {
        new LapSummary { LapNumber = 7, FuelUsedLitres = 2.01f }
    });

    Assert.Equal("多圈燃油趋势", panel.Title);
    Assert.Equal("L", panel.YAxisLabel);
    Assert.DoesNotContain("kg", panel.Title + panel.YAxisLabel, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 3: Run the chart-focused tests**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CurrentLapChartBuilderTests|FullyQualifiedName~TrendChartBuilderTests|FullyQualifiedName~LapAnalyzerTests"
```

Expected: PASS, proving current-lap data, empty states, tyre wear trends, and litres naming all hold.

- [ ] **Step 4: Run the full milestone verification**

Run:

```powershell
C:\Users\10670\.dotnet\dotnet.exe build .\F1Telemetry.sln -c Debug --no-restore
C:\Users\10670\.dotnet\dotnet.exe test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -c Debug --no-build
```

Expected: PASS for the whole solution and test suite.

- [ ] **Step 5: Commit the verification fixes**

```bash
git add F1Telemetry.Tests F1Telemetry.App F1Telemetry.Analytics F1Telemetry.Core
git commit -m "test: verify milestone 10 chart data flow"
```

## Self-Review

- Spec coverage:
  - Current-lap speed chart: Task 2 + Task 4
  - Current-lap throttle/brake chart: Task 2 + Task 4
  - Multi-lap fuel trend with litres semantics: Task 2 + Task 5
  - Multi-lap four-wheel wear trend: Task 1 + Task 2 + Task 4
  - No raw UDP binding in UI: Task 1 + Task 2
  - Throttled refresh and trend-only redraw on new lap: Task 4
  - Empty-state display: Task 3 + Task 5
- Build and test verification: Task 3 + Task 4 + Task 5
- Immutable current-lap snapshots: Task 1
- `ChartPanelViewModel.UpdateFrom` notifications: Task 3
- Null-safe tyre wear trends: Task 2 + Task 5
- Peak-preserving down-sampling and no full `Clear()` redraw: Task 2 + Task 3
- Placeholder scan:
  - No `TODO`, `TBD`, or “implement later” placeholders remain.
  - Each task lists concrete files, targeted tests, and run commands.
- Type consistency:
  - Per-wheel wear uses existing `WheelSet<float>` everywhere.
  - Fuel trend uses `FuelUsedLitres` everywhere.
  - Current-lap export uses `CaptureCurrentLapSamples()` consistently across tasks.
  - Chart changes stay within `Analytics`, `App\Charts`, `ViewModels`, `Controls`, `MainWindow`, plus the minimal `F1Telemetry.App.csproj` dependency change.
