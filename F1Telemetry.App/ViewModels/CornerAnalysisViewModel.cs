using System.Collections.ObjectModel;
using System.Windows.Input;
using F1Telemetry.Analytics.Corners;
using F1Telemetry.Analytics.Laps;
using F1Telemetry.Analytics.Tracks;
using F1Telemetry.Core.Abstractions;
using F1Telemetry.Storage.Interfaces;
using F1Telemetry.Storage.Models;
using F1Telemetry.Udp.Packets;

namespace F1Telemetry.App.ViewModels;

/// <summary>
/// Drives the V3 corner analysis page from persisted history session samples.
/// </summary>
public sealed class CornerAnalysisViewModel : ViewModelBase
{
    private readonly ILapSampleRepository _lapSampleRepository;
    private readonly ITrackSegmentMapProvider _trackSegmentMapProvider;
    private readonly CornerMetricsExtractor _cornerMetricsExtractor;
    private readonly RelayCommand _refreshCommand;
    private bool _isLoading;
    private string _statusText = "请选择历史会话后刷新弯角分析。";
    private string _emptyStateText = "等待历史会话和圈采样数据。";

    /// <summary>
    /// Initializes a new corner analysis ViewModel.
    /// </summary>
    /// <param name="historyBrowser">The shared history browser.</param>
    /// <param name="lapSampleRepository">The stored lap sample repository.</param>
    /// <param name="trackSegmentMapProvider">Optional segment map provider.</param>
    /// <param name="cornerMetricsExtractor">Optional metrics extractor.</param>
    public CornerAnalysisViewModel(
        HistorySessionBrowserViewModel historyBrowser,
        ILapSampleRepository lapSampleRepository,
        ITrackSegmentMapProvider? trackSegmentMapProvider = null,
        CornerMetricsExtractor? cornerMetricsExtractor = null)
    {
        HistoryBrowser = historyBrowser ?? throw new ArgumentNullException(nameof(historyBrowser));
        _lapSampleRepository = lapSampleRepository ?? throw new ArgumentNullException(nameof(lapSampleRepository));
        _trackSegmentMapProvider = trackSegmentMapProvider ?? new StaticTrackSegmentMapProvider();
        _cornerMetricsExtractor = cornerMetricsExtractor ?? new CornerMetricsExtractor();
        _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading);
        CornerRows = new ObservableCollection<CornerSummaryRowViewModel>();
    }

    /// <summary>
    /// Gets the command that refreshes corner analysis.
    /// </summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>
    /// Gets the shared history browser.
    /// </summary>
    public HistorySessionBrowserViewModel HistoryBrowser { get; }

    /// <summary>
    /// Gets corner analysis rows.
    /// </summary>
    public ObservableCollection<CornerSummaryRowViewModel> CornerRows { get; }

    /// <summary>
    /// Gets a value indicating whether data is loading.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the page status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the empty-state text.
    /// </summary>
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    /// <summary>
    /// Refreshes the selected session corner analysis.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        CornerRows.Clear();
        try
        {
            if (HistoryBrowser.SelectedSession is null)
            {
                await HistoryBrowser.RefreshSessionsAsync(cancellationToken);
            }

            var session = HistoryBrowser.SelectedSession;
            if (session is null)
            {
                EmptyStateText = "没有可用于弯角分析的历史会话。";
                StatusText = EmptyStateText;
                return;
            }

            var lapNumber = SelectLapNumber();
            if (lapNumber is null)
            {
                EmptyStateText = "该会话没有可用于弯角分析的历史圈记录。";
                StatusText = EmptyStateText;
                return;
            }

            var map = _trackSegmentMapProvider.GetMap(NormalizeTrackId(session.TrackId));
            if (map.Status == TrackSegmentMapStatus.Unsupported)
            {
                EmptyStateText = "该赛道暂未支持弯角分析。";
                StatusText = map.StatusReason ?? EmptyStateText;
                return;
            }

            var storedSamples = await _lapSampleRepository.GetForLapAsync(session.SessionId, lapNumber.Value, cancellationToken);
            if (storedSamples.Count == 0)
            {
                EmptyStateText = "该圈没有保存高频采样，无法生成弯角分析。";
                StatusText = EmptyStateText;
                return;
            }

            var result = _cornerMetricsExtractor.Extract(map, storedSamples.Select(ToLapSample).ToArray());
            foreach (var row in result.Corners.Select(CornerSummaryRowViewModel.FromSummary))
            {
                CornerRows.Add(row);
            }

            EmptyStateText = CornerRows.Count == 0 ? "没有可显示的弯角摘要。" : string.Empty;
            StatusText = $"已生成 Lap {lapNumber.Value} 弯角分析：{CornerRows.Count} 个弯角，置信度 {result.Confidence}。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private int? SelectLapNumber()
    {
        return HistoryBrowser.HistoryLaps
            .Where(lap => lap.LapNumber > 0)
            .OrderByDescending(lap => lap.LapNumber)
            .Select(lap => (int?)lap.LapNumber)
            .FirstOrDefault();
    }

    private static sbyte? NormalizeTrackId(int? trackId)
    {
        return trackId is >= sbyte.MinValue and <= sbyte.MaxValue ? (sbyte)trackId.Value : null;
    }

    private static LapSample ToLapSample(StoredLapSample sample)
    {
        return new LapSample
        {
            SampledAt = sample.SampledAt,
            FrameIdentifier = checked((uint)sample.FrameIdentifier),
            LapNumber = sample.LapNumber,
            LapDistance = sample.LapDistance,
            TotalDistance = sample.TotalDistance,
            CurrentLapTimeInMs = sample.CurrentLapTimeInMs is null ? null : checked((uint)sample.CurrentLapTimeInMs.Value),
            LastLapTimeInMs = sample.LastLapTimeInMs is null ? null : checked((uint)sample.LastLapTimeInMs.Value),
            SpeedKph = sample.SpeedKph,
            Throttle = sample.Throttle,
            Brake = sample.Brake,
            Steering = sample.Steering,
            Gear = sample.Gear is null ? null : checked((sbyte)sample.Gear.Value),
            FuelRemaining = sample.FuelRemainingLitres,
            FuelLapsRemaining = sample.FuelLapsRemaining,
            ErsStoreEnergy = sample.ErsStoreEnergy,
            TyreWear = sample.TyreWear,
            TyreWearPerWheel = sample.TyreWearFrontLeft is null
                || sample.TyreWearFrontRight is null
                || sample.TyreWearRearLeft is null
                || sample.TyreWearRearRight is null
                    ? null
                    : new WheelSet<float>(
                        sample.TyreWearRearLeft.Value,
                        sample.TyreWearRearRight.Value,
                        sample.TyreWearFrontLeft.Value,
                        sample.TyreWearFrontRight.Value),
            Position = sample.Position is null ? null : checked((byte)sample.Position.Value),
            DeltaFrontInMs = sample.DeltaFrontInMs is null ? null : checked((ushort)sample.DeltaFrontInMs.Value),
            DeltaLeaderInMs = sample.DeltaLeaderInMs is null ? null : checked((ushort)sample.DeltaLeaderInMs.Value),
            PitStatus = sample.PitStatus is null ? null : checked((byte)sample.PitStatus.Value),
            IsValid = sample.IsValid,
            VisualTyreCompound = sample.VisualTyreCompound is null ? null : checked((byte)sample.VisualTyreCompound.Value),
            ActualTyreCompound = sample.ActualTyreCompound is null ? null : checked((byte)sample.ActualTyreCompound.Value)
        };
    }
}
