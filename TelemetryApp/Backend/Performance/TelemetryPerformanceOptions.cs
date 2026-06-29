namespace TelemetryApp.Backend.Performance;

public sealed class TelemetryPerformanceOptions
{
    public const string SmoothProfile = "Płynny";
    public const string BalancedProfile = "Zbalansowany";
    public const string AccurateProfile = "Dokładny";
    public const string CustomProfile = "Własny";

    public string Profile { get; set; } = BalancedProfile;

    /// <summary>
    /// Maksymalna liczba odświeżeń UI dashboardu na sekundę.
    /// Niższe FPS = mniejsze użycie CPU.
    /// </summary>
    public int DashboardFps { get; set; } = 12;

    /// <summary>
    /// Minimalny odstęp między punktami jednego sygnału w historii dashboardu.
    /// To NIE wpływa na .rfdlog ani eksport.
    /// </summary>
    public int MinSampleIntervalMs { get; set; } = 150;

    /// <summary>
    /// Ile minut historii live trzymamy w RAM dla dashboardu.
    /// To NIE wpływa na .rfdlog ani eksport.
    /// </summary>
    public int DashboardHistoryMinutes { get; set; } = 20;

    /// <summary>
    /// Twardy limit punktów jednego sygnału w historii dashboardu.
    /// Realna długość historii to minimum z:
    /// DashboardHistoryMinutes oraz MaxSamplesPerSignal * MinSampleIntervalMs.
    /// </summary>
    public int MaxSamplesPerSignal { get; set; } = 9000;

    /// <summary>
    /// Docelowy limit punktów używany przy odświeżaniu/ładowaniu historii wykresu.
    /// Ważne: tego NIE będziemy używać do agresywnego ucinania live zakresu.
    /// </summary>
    public int MaxRenderedPointsPerChart { get; set; } = 2000;

    public bool PauseDashboardWhenHidden { get; set; } = true;

    public bool LimitDetachedChartsSameAsDashboard { get; set; } = true;

    public bool DisableLineSmoothnessWhenHeavy { get; set; } = true;

    public static IReadOnlyList<string> Profiles { get; } =
    [
        SmoothProfile,
        BalancedProfile,
        AccurateProfile,
        CustomProfile
    ];

    public static TelemetryPerformanceOptions CreateDefault()
    {
        return CreateForProfile(BalancedProfile);
    }

    public static TelemetryPerformanceOptions CreateForProfile(string? profile)
    {
        return profile switch
        {
            SmoothProfile => new TelemetryPerformanceOptions
            {
                Profile = SmoothProfile,
                DashboardFps = 8,
                MinSampleIntervalMs = 250,
                DashboardHistoryMinutes = 10,
                MaxSamplesPerSignal = 3000,
                MaxRenderedPointsPerChart = 1200,
                PauseDashboardWhenHidden = true,
                LimitDetachedChartsSameAsDashboard = true,
                DisableLineSmoothnessWhenHeavy = true
            },

            AccurateProfile => new TelemetryPerformanceOptions
            {
                Profile = AccurateProfile,
                DashboardFps = 20,
                MinSampleIntervalMs = 100,
                DashboardHistoryMinutes = 30,
                MaxSamplesPerSignal = 20000,
                MaxRenderedPointsPerChart = 4000,
                PauseDashboardWhenHidden = true,
                LimitDetachedChartsSameAsDashboard = true,
                DisableLineSmoothnessWhenHeavy = true
            },

            CustomProfile => new TelemetryPerformanceOptions
            {
                Profile = CustomProfile,
                DashboardFps = 12,
                MinSampleIntervalMs = 150,
                DashboardHistoryMinutes = 20,
                MaxSamplesPerSignal = 9000,
                MaxRenderedPointsPerChart = 2000,
                PauseDashboardWhenHidden = true,
                LimitDetachedChartsSameAsDashboard = true,
                DisableLineSmoothnessWhenHeavy = true
            },

            _ => new TelemetryPerformanceOptions
            {
                Profile = BalancedProfile,
                DashboardFps = 12,
                MinSampleIntervalMs = 150,
                DashboardHistoryMinutes = 20,
                MaxSamplesPerSignal = 9000,
                MaxRenderedPointsPerChart = 2000,
                PauseDashboardWhenHidden = true,
                LimitDetachedChartsSameAsDashboard = true,
                DisableLineSmoothnessWhenHeavy = true
            }
        };
    }

    public TelemetryPerformanceOptions Clone()
    {
        return new TelemetryPerformanceOptions
        {
            Profile = Profile,
            DashboardFps = DashboardFps,
            MinSampleIntervalMs = MinSampleIntervalMs,
            DashboardHistoryMinutes = DashboardHistoryMinutes,
            MaxSamplesPerSignal = MaxSamplesPerSignal,
            MaxRenderedPointsPerChart = MaxRenderedPointsPerChart,
            PauseDashboardWhenHidden = PauseDashboardWhenHidden,
            LimitDetachedChartsSameAsDashboard = LimitDetachedChartsSameAsDashboard,
            DisableLineSmoothnessWhenHeavy = DisableLineSmoothnessWhenHeavy
        };
    }

    public void Normalize()
    {
        if (!Profiles.Contains(Profile))
            Profile = CustomProfile;

        DashboardFps = Math.Clamp(DashboardFps, 2, 30);
        MinSampleIntervalMs = Math.Clamp(MinSampleIntervalMs, 20, 1000);
        DashboardHistoryMinutes = Math.Clamp(DashboardHistoryMinutes, 1, 120);
        MaxSamplesPerSignal = Math.Clamp(MaxSamplesPerSignal, 500, 50000);
        MaxRenderedPointsPerChart = Math.Clamp(MaxRenderedPointsPerChart, 300, 10000);
    }

    public double EffectiveHistoryMinutesBySampleLimit()
    {
        var totalMs = (double)MaxSamplesPerSignal * MinSampleIntervalMs;
        return totalMs / 1000.0 / 60.0;
    }

    public double EffectiveDashboardHistoryMinutes()
    {
        return Math.Min(DashboardHistoryMinutes, EffectiveHistoryMinutesBySampleLimit());
    }

    public string BuildSummary()
    {
        return
            $"Profil: {Profile}, " +
            $"FPS: {DashboardFps}, " +
            $"odstęp próbek: {MinSampleIntervalMs} ms, " +
            $"historia: {DashboardHistoryMinutes} min, " +
            $"próbek/sygnał: {MaxSamplesPerSignal}, " +
            $"punktów/wykres: {MaxRenderedPointsPerChart}";
    }
}