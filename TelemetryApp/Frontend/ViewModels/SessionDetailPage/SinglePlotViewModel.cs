using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private const double MinimumWindowSeconds = 1.0;

    private readonly ObservableCollection<ObservablePoint> _points = new();
    private readonly LineSeries<ObservablePoint> _lineSeries;
    private readonly Axis _xAxis;
    private readonly Axis _yAxis;
    private readonly List<CanSignalOption> _availableSignals = new();

    private ulong? _firstTimestampMs;
    private bool _suppressRangeChanged;
    private bool _suppressSignalSearchTextChanged;
    private bool _hasManualRangeSelection;

    public event Action<SinglePlotViewModel>? SelectedSignalChanged;

    [ObservableProperty]
    private string title = "Nowy wykres";

    [ObservableProperty]
    private CanSignalOption? selectedSignal;

    [ObservableProperty]
    private string signalSearchText = string.Empty;

    [ObservableProperty]
    private bool isSignalSearchOpen;

    [ObservableProperty]
    private string lastValueText = "Brak danych";

    [ObservableProperty]
    private double rangeStartSeconds;

    [ObservableProperty]
    private double rangeEndSeconds = MinimumWindowSeconds;

    [ObservableProperty]
    private double timelineMaximumSeconds = MinimumWindowSeconds;

    [ObservableProperty]
    private bool hasTimeline;

    [ObservableProperty]
    private bool isFollowingLive = true;

    [ObservableProperty]
    private string scrollText = "Zakres: brak danych";

    public ObservableCollection<CanSignalOption> FilteredSignals { get; } = new();

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
            MaxLimit = MinimumWindowSeconds,
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

    public void SetAvailableSignals(IEnumerable<CanSignalOption> signals)
    {
        _availableSignals.Clear();
        _availableSignals.AddRange(signals);

        RefreshFilteredSignals();
    }

    partial void OnSignalSearchTextChanged(string value)
    {
        if (_suppressSignalSearchTextChanged)
            return;

        IsSignalSearchOpen = true;
        RefreshFilteredSignals();
    }

    partial void OnSelectedSignalChanged(CanSignalOption? value)
    {
        _points.Clear();
        _firstTimestampMs = null;
        _hasManualRangeSelection = false;

        IsFollowingLive = true;
        SetTimelineMaximum(MinimumWindowSeconds);
        SetRangeInternal(0, MinimumWindowSeconds, keepLiveMode: true);

        _suppressSignalSearchTextChanged = true;
        SignalSearchText = value?.DisplayName ?? string.Empty;
        _suppressSignalSearchTextChanged = false;

        IsSignalSearchOpen = false;

        while (Sections.Count > 1) 
            Sections.RemoveAt(1);

        if (value is not null)
        {
            if (value.SafeMin.HasValue) Sections.Add(new RectangularSection { Yi = value.SafeMin.Value, Yj = value.SafeMin.Value, Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2) { PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect(new float[] { 8, 16 }) } });
            if (value.SafeMax.HasValue) Sections.Add(new RectangularSection { Yi = value.SafeMax.Value, Yj = value.SafeMax.Value, Stroke = new SolidColorPaint(SKColors.Red, 2) { PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect(new float[] { 8, 16 }) } });
        }

        if (value is null)
        {
            Title = "Nowy wykres";
            _lineSeries.Name = "Wybierz sygnał";
            _yAxis.Name = "Wartość";
            LastValueText = "Brak danych";
            ScrollText = "Zakres: brak danych";
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

    [RelayCommand]
    public void OpenSignalSearch()
    {
        IsSignalSearchOpen = true;
        RefreshFilteredSignals();
    }

    [RelayCommand]
    public void ClearSignalSearch()
    {
        SelectedSignal = null;
        SignalSearchText = string.Empty;
        IsSignalSearchOpen = true;
        RefreshFilteredSignals();
    }

    public void SelectSignal(CanSignalOption option)
    {
        SelectedSignal = option;
    }

    private void RefreshFilteredSignals()
    {
        var query = SignalSearchText?.Trim() ?? string.Empty;

        var result = _availableSignals
            .Where(option => MatchesSearch(option, query))
            .ToList();

        FilteredSignals.Clear();

        foreach (var option in result)
            FilteredSignals.Add(option);
    }

    private static bool MatchesSearch(CanSignalOption option, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var tokens = query.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var found =
                option.DisplayName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                option.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                option.Key.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                $"0x{option.CanId:X3}".Contains(token, StringComparison.OrdinalIgnoreCase) ||
                option.CanId.ToString().Contains(token, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(option.Unit) &&
                 option.Unit.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(option.FrameSummary) &&
                 option.FrameSummary.Contains(token, StringComparison.OrdinalIgnoreCase));

            if (!found)
                return false;
        }

        return true;
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

        _hasManualRangeSelection = true;

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

        _hasManualRangeSelection = true;

        UpdateLiveModeFromRange();
    }

    public void LoadHistory(IReadOnlyList<SignalSample> samples)
    {
        _points.Clear();
        _firstTimestampMs = null;
        _hasManualRangeSelection = false;

        IsFollowingLive = true;
        SetTimelineMaximum(MinimumWindowSeconds);
        SetRangeInternal(0, MinimumWindowSeconds, keepLiveMode: true);

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

        if (_hasManualRangeSelection)
            MoveRangeToLatestWindow();
        else
            MoveRangeToFullData();

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
            SetTimelineMaximum(MinimumWindowSeconds);
            SetRangeInternal(0, MinimumWindowSeconds, keepLiveMode: true);
            RefreshAxisWindow();
            return;
        }

        var totalDataSeconds = Math.Max(MinimumWindowSeconds, _points[^1].X ?? 0);

        SetTimelineMaximum(totalDataSeconds);

        if (followLive)
        {
            if (_hasManualRangeSelection)
                MoveRangeToLatestWindow();
            else
                MoveRangeToFullData();
        }
        else
        {
            SetRangeInternal(
                Math.Clamp(RangeStartSeconds, 0, TimelineMaximumSeconds),
                Math.Clamp(RangeEndSeconds, 0, TimelineMaximumSeconds),
                keepLiveMode: false);
        }

        RefreshAxisWindow();
    }

    private void MoveRangeToFullData()
    {
        var dataEnd = _points.Count == 0
            ? MinimumWindowSeconds
            : Math.Max(MinimumWindowSeconds, _points[^1].X ?? 0);

        var end = Math.Min(TimelineMaximumSeconds, dataEnd);

        SetRangeInternal(0, end, keepLiveMode: true);
    }

    private void MoveRangeToLatestWindow()
    {
        var currentWindowSeconds = GetCurrentWindowSeconds();

        var dataEnd = _points.Count == 0
            ? MinimumWindowSeconds
            : Math.Max(MinimumWindowSeconds, _points[^1].X ?? 0);

        var end = Math.Min(TimelineMaximumSeconds, dataEnd);
        var start = Math.Max(0, end - currentWindowSeconds);

        SetRangeInternal(start, end, keepLiveMode: true);
    }

    private double GetCurrentWindowSeconds()
    {
        var currentWindowSeconds = RangeEndSeconds - RangeStartSeconds;

        if (double.IsNaN(currentWindowSeconds) ||
            double.IsInfinity(currentWindowSeconds) ||
            currentWindowSeconds < MinimumWindowSeconds)
        {
            return MinimumWindowSeconds;
        }

        return Math.Clamp(
            currentWindowSeconds,
            MinimumWindowSeconds,
            TimelineMaximumSeconds);
    }

    private void SetTimelineMaximum(double value)
    {
        TimelineMaximumSeconds = Math.Max(MinimumWindowSeconds, value);
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
        IsFollowingLive = Math.Abs(RangeEndSeconds - TimelineMaximumSeconds) < 0.25;
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