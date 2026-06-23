using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace TelemetryApp.Frontend.ViewModels;

public partial class SinglePlotViewModel : ObservableObject
{
    private const double DefaultLiveWindowSeconds = 60.0;
    private const double MinimumWindowSeconds = 1.0;

    private readonly ObservableCollection<ObservablePoint> _points = new();
    private readonly LineSeries<ObservablePoint> _lineSeries;
    private readonly Axis _xAxis;
    private readonly Axis _yAxis;

    private ulong? _firstTimestampMs;
    private bool _suppressRangeChanged;

    public event Action<SinglePlotViewModel>? SelectedSignalChanged;

    [ObservableProperty]
    private string title = "Nowy wykres";

    [ObservableProperty]
    private CanSignalOption? selectedSignal;

    [ObservableProperty]
    private string lastValueText = "Brak danych";

    [ObservableProperty]
    private double rangeStartSeconds;

    [ObservableProperty]
    private double rangeEndSeconds = DefaultLiveWindowSeconds;

    [ObservableProperty]
    private double timelineMaximumSeconds = DefaultLiveWindowSeconds;

    [ObservableProperty]
    private bool hasTimeline;

    [ObservableProperty]
    private bool isFollowingLive = true;

    [ObservableProperty]
    private string scrollText = "Zakres: ostatnia minuta";

    public ObservableCollection<ISeries> Series { get; }
    public ObservableCollection<Axis> XAxes { get; }
    public ObservableCollection<Axis> YAxes { get; }
    public ObservableCollection<RectangularSection> Sections { get; }

    public SinglePlotViewModel()
    {
        var labelPaint = new SolidColorPaint(new SKColor(210, 218, 230));
        var separatorPaint = new SolidColorPaint(new SKColor(71, 85, 105, 120), 1);

        _lineSeries = new LineSeries<ObservablePoint>
        {
            Values = _points,
            Name = "Wybierz sygnał",
            Fill = null,
            Stroke = new SolidColorPaint(new SKColor(56, 189, 248), 3),
            GeometrySize = 0,
            GeometryStroke = null,
            GeometryFill = null,
            LineSmoothness = 0.18
        };

        _xAxis = new Axis
        {
            Name = "Czas",
            MinLimit = 0,
            MaxLimit = DefaultLiveWindowSeconds,
            Labeler = FormatAxisSeconds,
            LabelsPaint = labelPaint,
            NamePaint = labelPaint,
            SeparatorsPaint = separatorPaint,
            TextSize = 12
        };

        _yAxis = new Axis
        {
            Name = "Wartość",
            LabelsPaint = labelPaint,
            NamePaint = labelPaint,
            SeparatorsPaint = separatorPaint,
            TextSize = 12
        };

        Series = new ObservableCollection<ISeries> { _lineSeries };
        XAxes = new ObservableCollection<Axis> { _xAxis };
        YAxes = new ObservableCollection<Axis> { _yAxis };

        Sections = new ObservableCollection<RectangularSection>
        {
            new RectangularSection
            {
                Yi = 5,
                Yj = 5,
                Stroke = new SolidColorPaint(new SKColor(250, 204, 21), 2)
            }
        };
    }

    partial void OnSelectedSignalChanged(CanSignalOption? value)
    {
        _points.Clear();
        _firstTimestampMs = null;
        IsFollowingLive = true;
        SetTimelineMaximum(DefaultLiveWindowSeconds);
        SetRangeInternal(0, DefaultLiveWindowSeconds, keepLiveMode: true);

        if (value is null)
        {
            Title = "Nowy wykres";
            _lineSeries.Name = "Wybierz sygnał";
            _yAxis.Name = "Wartość";
            LastValueText = "Brak danych";
            ScrollText = "Zakres: ostatnia minuta";
        }
        else
        {
            Title = value.DisplayName;
            _lineSeries.Name = value.Name;
            _yAxis.Name = string.IsNullOrWhiteSpace(value.Unit) ? "Wartość" : value.Unit;
            LastValueText = "Czekam na dane...";
            ScrollText = "Zakres: czekam na dane";
        }

        SelectedSignalChanged?.Invoke(this);
    }

    partial void OnRangeStartSecondsChanged(double value)
    {
        if (_suppressRangeChanged)
            return;

        var start = Math.Clamp(value, 0, TimelineMaximumSeconds);
        var end = Math.Clamp(RangeEndSeconds, 0, TimelineMaximumSeconds);

        if (end - start < MinimumWindowSeconds)
            start = Math.Max(0, end - MinimumWindowSeconds);

        SetRangeInternal(start, end, keepLiveMode: false);
        UpdateLiveModeFromRange();
    }

    partial void OnRangeEndSecondsChanged(double value)
    {
        if (_suppressRangeChanged)
            return;

        var start = Math.Clamp(RangeStartSeconds, 0, TimelineMaximumSeconds);
        var end = Math.Clamp(value, 0, TimelineMaximumSeconds);

        if (end - start < MinimumWindowSeconds)
            end = Math.Min(TimelineMaximumSeconds, start + MinimumWindowSeconds);

        SetRangeInternal(start, end, keepLiveMode: false);
        UpdateLiveModeFromRange();
    }

    public void LoadHistory(IReadOnlyList<SignalSample> samples)
    {
        _points.Clear();
        _firstTimestampMs = null;
        IsFollowingLive = true;
        SetTimelineMaximum(DefaultLiveWindowSeconds);
        SetRangeInternal(0, DefaultLiveWindowSeconds, keepLiveMode: true);

        if (samples.Count == 0)
        {
            LastValueText = SelectedSignal is null ? "Brak danych" : "Czekam na dane...";
            ScrollText = "Zakres: czekam na dane";
            return;
        }

        foreach (var sample in samples)
            AddSampleCore(sample, updateWindow: false);

        var last = samples[^1];
        UpdateLastValueText(last.Value, last.Unit, last.TimestampMs);
        UpdateTimelineLimits(followLive: true);
    }

    public void AddSample(SignalSample sample)
    {
        if (SelectedSignal is null || !SelectedSignal.Matches(sample.Key, sample.CanId))
            return;

        AddSampleCore(sample, updateWindow: true);
    }

    [RelayCommand]
    private void JumpToLive()
    {
        IsFollowingLive = true;
        MoveRangeToLatestMinute();
        RefreshAxisWindow();
    }

    private void AddSampleCore(SignalSample sample, bool updateWindow)
    {
        _firstTimestampMs ??= sample.TimestampMs;

        var x = ToSecondsFromStart(sample.TimestampMs);
        _points.Add(new ObservablePoint(x, sample.Value));

        UpdateLastValueText(sample.Value, sample.Unit, sample.TimestampMs);

        if (updateWindow)
            UpdateTimelineLimits(followLive: IsFollowingLive);
    }

    private void UpdateTimelineLimits(bool followLive)
    {
        if (_points.Count == 0)
        {
            SetTimelineMaximum(DefaultLiveWindowSeconds);
            SetRangeInternal(0, DefaultLiveWindowSeconds, keepLiveMode: true);
            RefreshAxisWindow();
            return;
        }

        var totalDataSeconds = _points[^1].X ?? 0;
        SetTimelineMaximum(Math.Max(DefaultLiveWindowSeconds, totalDataSeconds));

        if (followLive)
            MoveRangeToLatestMinute();
        else
            SetRangeInternal(
                Math.Clamp(RangeStartSeconds, 0, TimelineMaximumSeconds),
                Math.Clamp(RangeEndSeconds, 0, TimelineMaximumSeconds),
                keepLiveMode: false);

        RefreshAxisWindow();
    }

    private void MoveRangeToLatestMinute()
    {
        var dataEnd = _points.Count == 0 ? DefaultLiveWindowSeconds : Math.Max(DefaultLiveWindowSeconds, _points[^1].X ?? 0);
        var end = Math.Min(TimelineMaximumSeconds, dataEnd);
        var start = Math.Max(0, end - DefaultLiveWindowSeconds);

        SetRangeInternal(start, end, keepLiveMode: true);
    }

    private void SetTimelineMaximum(double value)
    {
        TimelineMaximumSeconds = Math.Max(DefaultLiveWindowSeconds, value);
        HasTimeline = TimelineMaximumSeconds > MinimumWindowSeconds;
    }

    private void SetRangeInternal(double start, double end, bool keepLiveMode)
    {
        start = Math.Clamp(start, 0, TimelineMaximumSeconds);
        end = Math.Clamp(end, 0, TimelineMaximumSeconds);

        if (end < start)
            (start, end) = (end, start);

        if (end - start < MinimumWindowSeconds)
        {
            if (end >= TimelineMaximumSeconds)
                start = Math.Max(0, end - MinimumWindowSeconds);
            else
                end = Math.Min(TimelineMaximumSeconds, start + MinimumWindowSeconds);
        }

        _suppressRangeChanged = true;
        RangeStartSeconds = start;
        RangeEndSeconds = end;
        _suppressRangeChanged = false;

        if (keepLiveMode)
            IsFollowingLive = true;

        RefreshAxisWindow();
    }

    private void UpdateLiveModeFromRange()
    {
        IsFollowingLive = Math.Abs(RangeEndSeconds - TimelineMaximumSeconds) < 0.25
            && Math.Abs((RangeEndSeconds - RangeStartSeconds) - DefaultLiveWindowSeconds) < 0.5;
    }

    private void RefreshAxisWindow()
    {
        var start = Math.Clamp(RangeStartSeconds, 0, TimelineMaximumSeconds);
        var end = Math.Clamp(RangeEndSeconds, 0, TimelineMaximumSeconds);

        if (end <= start)
            end = Math.Min(TimelineMaximumSeconds, start + MinimumWindowSeconds);

        _xAxis.MinLimit = start;
        _xAxis.MaxLimit = end;

        var dataEnd = _points.Count == 0 ? 0 : _points[^1].X ?? 0;
        ScrollText = _points.Count == 0
            ? "Zakres: brak danych"
            : $"Zakres: {FormatAxisSeconds(start)} – {FormatAxisSeconds(end)} | dane do {FormatAxisSeconds(dataEnd)}";
    }

    private double ToSecondsFromStart(ulong timestampMs)
    {
        if (_firstTimestampMs is null)
            return 0;

        if (timestampMs <= _firstTimestampMs.Value)
            return 0;

        return (timestampMs - _firstTimestampMs.Value) / 1000.0;
    }

    private void UpdateLastValueText(double value, string? unit, ulong timestampMs)
    {
        var unitText = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
        LastValueText = $"Ostatnia wartość: {value:0.###}{unitText}  |  {FormatTimestamp(timestampMs)}";
    }

    private static string FormatAxisSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            seconds = 0;

        var time = TimeSpan.FromSeconds(seconds);

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static string FormatTimestamp(ulong timestampMs)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unchecked((long)timestampMs))
                .ToLocalTime()
                .ToString("HH:mm:ss.fff");
        }
        catch
        {
            return $"timestamp={timestampMs}";
        }
    }
}
