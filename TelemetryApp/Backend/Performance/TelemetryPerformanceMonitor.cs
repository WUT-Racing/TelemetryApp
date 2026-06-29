using System.Diagnostics;

namespace TelemetryApp.Backend.Performance;

public sealed class TelemetryPerformanceMonitor
{
    private readonly Process _process = Process.GetCurrentProcess();

    private DateTime _lastCpuSampleTimeUtc = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;
    private double _lastCpuPercent;

    public TelemetryPerformanceMonitor()
    {
        try
        {
            _lastTotalProcessorTime = _process.TotalProcessorTime;
        }
        catch
        {
            _lastTotalProcessorTime = TimeSpan.Zero;
        }
    }

    public TelemetryPerformanceSnapshot GetSnapshot(
        TelemetryPerformanceOptions options,
        int activeChartCount,
        int detachedChartCount,
        int dashboardHistoryPointCount,
        int trackedSignalCount)
    {
        options.Normalize();

        var cpuPercent = ReadCpuPercent();
        var ramMb = ReadRamMb();

        var estimatedMemoryMb = EstimateDashboardMemoryMb(options, trackedSignalCount);
        var renderCost = EstimateRenderCost(options, activeChartCount + detachedChartCount);
        var memoryCost = EstimateMemoryCost(estimatedMemoryMb);
        var recommendation = BuildRecommendation(options, activeChartCount + detachedChartCount, renderCost, memoryCost);

        return new TelemetryPerformanceSnapshot(
            CpuPercent: cpuPercent,
            RamMb: ramMb,
            ActiveChartCount: activeChartCount,
            DetachedChartCount: detachedChartCount,
            DashboardHistoryPointCount: dashboardHistoryPointCount,
            TrackedSignalCount: trackedSignalCount,
            EstimatedDashboardMemoryMb: estimatedMemoryMb,
            EstimatedRenderCost: renderCost,
            EstimatedMemoryCost: memoryCost,
            Recommendation: recommendation,
            EffectiveHistoryMinutes: options.EffectiveDashboardHistoryMinutes());
    }

    private double ReadCpuPercent()
    {
        try
        {
            _process.Refresh();

            var now = DateTime.UtcNow;
            var totalProcessorTime = _process.TotalProcessorTime;

            var elapsedMs = (now - _lastCpuSampleTimeUtc).TotalMilliseconds;
            var cpuElapsedMs = (totalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;

            _lastCpuSampleTimeUtc = now;
            _lastTotalProcessorTime = totalProcessorTime;

            if (elapsedMs <= 0)
                return _lastCpuPercent;

            var cpuPercent = cpuElapsedMs / (elapsedMs * Environment.ProcessorCount) * 100.0;

            if (double.IsNaN(cpuPercent) || double.IsInfinity(cpuPercent))
                return _lastCpuPercent;

            _lastCpuPercent = Math.Clamp(cpuPercent, 0, 100);
            return _lastCpuPercent;
        }
        catch
        {
            return _lastCpuPercent;
        }
    }

    private double ReadRamMb()
    {
        try
        {
            _process.Refresh();
            return _process.WorkingSet64 / 1024.0 / 1024.0;
        }
        catch
        {
            return 0;
        }
    }

    private static double EstimateDashboardMemoryMb(
        TelemetryPerformanceOptions options,
        int trackedSignalCount)
    {
        var safeSignalCount = Math.Max(1, trackedSignalCount);

        // Celowo ostrożne oszacowanie:
        // SignalSample + listy + narzut kolekcji + obiekty UI.
        const double estimatedBytesPerSampleWithOverhead = 128.0;

        var samples = safeSignalCount * options.MaxSamplesPerSignal;
        return samples * estimatedBytesPerSampleWithOverhead / 1024.0 / 1024.0;
    }

    private static string EstimateRenderCost(
        TelemetryPerformanceOptions options,
        int chartCount)
    {
        var safeChartCount = Math.Max(1, chartCount);

        var score =
            safeChartCount *
            options.MaxRenderedPointsPerChart *
            options.DashboardFps / 1000.0;

        if (score < 35)
            return "Niski";

        if (score < 100)
            return "Średni";

        if (score < 220)
            return "Wysoki";

        return "Bardzo wysoki";
    }

    private static string EstimateMemoryCost(double estimatedMemoryMb)
    {
        if (estimatedMemoryMb < 200)
            return "Niski";

        if (estimatedMemoryMb < 600)
            return "Średni";

        if (estimatedMemoryMb < 1200)
            return "Wysoki";

        return "Bardzo wysoki";
    }

    private static string BuildRecommendation(
        TelemetryPerformanceOptions options,
        int chartCount,
        string renderCost,
        string memoryCost)
    {
        if (chartCount >= 8 &&
            renderCost is "Wysoki" or "Bardzo wysoki")
        {
            return "Przy 8+ wykresach rozważ niższy FPS, większy odstęp próbek albo mniejszy limit punktów rysowanych.";
        }

        if (memoryCost is "Wysoki" or "Bardzo wysoki")
        {
            return "Szacowane użycie RAM dashboardu jest wysokie. Skróć historię albo zmniejsz limit próbek na sygnał.";
        }

        if (options.EffectiveDashboardHistoryMinutes() < options.DashboardHistoryMinutes - 0.1)
        {
            return "Uwaga: limit próbek na sygnał skraca realną historię dashboardu. Zwiększ limit próbek albo zwiększ odstęp próbkowania.";
        }

        if (options.DashboardFps >= 20 && options.MaxRenderedPointsPerChart >= 4000)
        {
            return "Ustawienia są dokładne, ale mogą obciążać wykresy. Jeśli pojawią się przycięcia, zmniejsz FPS albo limit punktów.";
        }

        return "Ustawienia wyglądają bezpiecznie.";
    }
}

public sealed record TelemetryPerformanceSnapshot(
    double CpuPercent,
    double RamMb,
    int ActiveChartCount,
    int DetachedChartCount,
    int DashboardHistoryPointCount,
    int TrackedSignalCount,
    double EstimatedDashboardMemoryMb,
    string EstimatedRenderCost,
    string EstimatedMemoryCost,
    string Recommendation,
    double EffectiveHistoryMinutes)
{
    public string CpuText => $"{CpuPercent:0.0}%";

    public string RamText => $"{RamMb:0} MB";

    public string ChartCountText => DetachedChartCount > 0
        ? $"{ActiveChartCount} w dashboardzie + {DetachedChartCount} odpięte"
        : ActiveChartCount.ToString();

    public string HistoryPointCountText => DashboardHistoryPointCount.ToString("N0");

    public string TrackedSignalCountText => TrackedSignalCount.ToString("N0");

    public string EstimatedDashboardMemoryText => $"~{EstimatedDashboardMemoryMb:0} MB";

    public string EffectiveHistoryText => $"~{EffectiveHistoryMinutes:0.0} min";

    public string EstimatedCostText =>
        $"CPU/render: {EstimatedRenderCost}, RAM historii: {EstimatedMemoryCost}";
}