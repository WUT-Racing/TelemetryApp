using Microsoft.Maui.Storage;

namespace TelemetryApp.Backend.Performance;

public sealed class TelemetryPerformanceSettingsStore
{
    private const string ProfileKey = "PerformanceProfile";
    private const string DashboardFpsKey = "PerformanceDashboardFps";
    private const string MinSampleIntervalMsKey = "PerformanceMinSampleIntervalMs";
    private const string DashboardHistoryMinutesKey = "PerformanceDashboardHistoryMinutes";
    private const string MaxSamplesPerSignalKey = "PerformanceMaxSamplesPerSignal";
    private const string MaxRenderedPointsPerChartKey = "PerformanceMaxRenderedPointsPerChart";
    private const string PauseDashboardWhenHiddenKey = "PerformancePauseDashboardWhenHidden";
    private const string LimitDetachedChartsSameAsDashboardKey = "PerformanceLimitDetachedChartsSameAsDashboard";
    private const string DisableLineSmoothnessWhenHeavyKey = "PerformanceDisableLineSmoothnessWhenHeavy";

    private TelemetryPerformanceOptions _currentOptions;

    public event Action<TelemetryPerformanceOptions>? OptionsChanged;

    public TelemetryPerformanceSettingsStore()
    {
        _currentOptions = LoadFromPreferences();
    }

    public TelemetryPerformanceOptions GetOptions()
    {
        return _currentOptions.Clone();
    }

    public void SaveOptions(TelemetryPerformanceOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        var normalized = options.Clone();
        normalized.Normalize();

        Preferences.Set(ProfileKey, normalized.Profile);
        Preferences.Set(DashboardFpsKey, normalized.DashboardFps);
        Preferences.Set(MinSampleIntervalMsKey, normalized.MinSampleIntervalMs);
        Preferences.Set(DashboardHistoryMinutesKey, normalized.DashboardHistoryMinutes);
        Preferences.Set(MaxSamplesPerSignalKey, normalized.MaxSamplesPerSignal);
        Preferences.Set(MaxRenderedPointsPerChartKey, normalized.MaxRenderedPointsPerChart);
        Preferences.Set(PauseDashboardWhenHiddenKey, normalized.PauseDashboardWhenHidden);
        Preferences.Set(LimitDetachedChartsSameAsDashboardKey, normalized.LimitDetachedChartsSameAsDashboard);
        Preferences.Set(DisableLineSmoothnessWhenHeavyKey, normalized.DisableLineSmoothnessWhenHeavy);

        _currentOptions = normalized;

        OptionsChanged?.Invoke(_currentOptions.Clone());
    }

    public void ApplyProfile(string profile)
    {
        SaveOptions(TelemetryPerformanceOptions.CreateForProfile(profile));
    }

    private static TelemetryPerformanceOptions LoadFromPreferences()
    {
        var defaults = TelemetryPerformanceOptions.CreateDefault();

        var options = new TelemetryPerformanceOptions
        {
            Profile = Preferences.Get(ProfileKey, defaults.Profile),
            DashboardFps = Preferences.Get(DashboardFpsKey, defaults.DashboardFps),
            MinSampleIntervalMs = Preferences.Get(MinSampleIntervalMsKey, defaults.MinSampleIntervalMs),
            DashboardHistoryMinutes = Preferences.Get(DashboardHistoryMinutesKey, defaults.DashboardHistoryMinutes),
            MaxSamplesPerSignal = Preferences.Get(MaxSamplesPerSignalKey, defaults.MaxSamplesPerSignal),
            MaxRenderedPointsPerChart = Preferences.Get(MaxRenderedPointsPerChartKey, defaults.MaxRenderedPointsPerChart),
            PauseDashboardWhenHidden = Preferences.Get(PauseDashboardWhenHiddenKey, defaults.PauseDashboardWhenHidden),
            LimitDetachedChartsSameAsDashboard = Preferences.Get(LimitDetachedChartsSameAsDashboardKey, defaults.LimitDetachedChartsSameAsDashboard),
            DisableLineSmoothnessWhenHeavy = Preferences.Get(DisableLineSmoothnessWhenHeavyKey, defaults.DisableLineSmoothnessWhenHeavy)
        };

        options.Normalize();

        return options;
    }
}