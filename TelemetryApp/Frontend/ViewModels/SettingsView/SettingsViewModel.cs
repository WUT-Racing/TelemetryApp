using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using TelemetryApp.Backend.Export;
using TelemetryApp.Backend.Performance;

namespace TelemetryApp.Frontend.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string ThemePreferenceKey = "AppTheme";
    private const string DriversPreferenceKey = "DriversList";

    private readonly TelemetryExportSettingsStore _exportSettingsStore;
    private readonly TelemetryPerformanceSettingsStore _performanceSettingsStore;
    private readonly TelemetryPerformanceMonitor _performanceMonitor;
    private readonly SessionDetailViewModel _sessionDetailViewModel;

    private bool _isLoadingExportOptions;
    private bool _isLoadingPerformanceFields;

    [ObservableProperty]
    private string currentTab = "General";

    [ObservableProperty]
    private string selectedTheme = "Ciemny";

    [ObservableProperty]
    private string newDriverName = string.Empty;

    [ObservableProperty]
    private bool exportLongCsv = true;

    [ObservableProperty]
    private bool exportWideCsv = true;

    [ObservableProperty]
    private bool exportRawFramesCsv = true;

    public ObservableCollection<Driver> Drivers { get; } = new();

    public ObservableCollection<string> PerformanceProfiles { get; } =
        new(TelemetryPerformanceOptions.Profiles);

    [ObservableProperty]
    private string selectedPerformanceProfile = TelemetryPerformanceOptions.BalancedProfile;

    [ObservableProperty]
    private string dashboardFpsText = "12";

    [ObservableProperty]
    private string minSampleIntervalMsText = "150";

    [ObservableProperty]
    private string dashboardHistoryMinutesText = "20";

    [ObservableProperty]
    private string maxSamplesPerSignalText = "9000";

    [ObservableProperty]
    private string maxRenderedPointsPerChartText = "2000";

    [ObservableProperty]
    private bool pauseDashboardWhenHidden = true;

    [ObservableProperty]
    private bool limitDetachedChartsSameAsDashboard = true;

    [ObservableProperty]
    private bool disableLineSmoothnessWhenHeavy = true;

    [ObservableProperty]
    private string performanceCurrentUsageText = "Kliknij „Odśwież pomiar”, żeby zobaczyć aktualne użycie CPU/RAM.";

    [ObservableProperty]
    private string performanceEstimateText = string.Empty;

    [ObservableProperty]
    private string performanceRecommendationText = string.Empty;

    [ObservableProperty]
    private string performanceStatusText = "Ustawienia zostaną zastosowane po kliknięciu „Zastosuj ustawienia”.";

    public bool IsLightTheme => SelectedTheme == "Jasny";
    public bool IsDarkTheme => SelectedTheme == "Ciemny";

    public SettingsViewModel(
        TelemetryExportSettingsStore exportSettingsStore,
        TelemetryPerformanceSettingsStore performanceSettingsStore,
        TelemetryPerformanceMonitor performanceMonitor,
        SessionDetailViewModel sessionDetailViewModel)
    {
        _exportSettingsStore = exportSettingsStore ?? throw new ArgumentNullException(nameof(exportSettingsStore));
        _performanceSettingsStore = performanceSettingsStore ?? throw new ArgumentNullException(nameof(performanceSettingsStore));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _sessionDetailViewModel = sessionDetailViewModel ?? throw new ArgumentNullException(nameof(sessionDetailViewModel));

        var savedTheme = Preferences.Get(ThemePreferenceKey, "Ciemny");
        ApplyTheme(savedTheme, save: false);

        LoadExportOptions();
        LoadPerformanceFields(_performanceSettingsStore.GetOptions());

        Drivers.CollectionChanged += OnDriversCollectionChanged;

        LoadDriversFromPreferences();

        RefreshPerformancePreview();
    }

    private void LoadExportOptions()
    {
        _isLoadingExportOptions = true;

        try
        {
            var options = _exportSettingsStore.GetOptions();

            ExportLongCsv = options.ExportLongCsv;
            ExportWideCsv = options.ExportWideCsv;
            ExportRawFramesCsv = options.ExportRawFramesCsv;
        }
        finally
        {
            _isLoadingExportOptions = false;
        }
    }

    private void LoadPerformanceFields(TelemetryPerformanceOptions options)
    {
        _isLoadingPerformanceFields = true;

        try
        {
            options.Normalize();

            SelectedPerformanceProfile = options.Profile;
            DashboardFpsText = options.DashboardFps.ToString(CultureInfo.InvariantCulture);
            MinSampleIntervalMsText = options.MinSampleIntervalMs.ToString(CultureInfo.InvariantCulture);
            DashboardHistoryMinutesText = options.DashboardHistoryMinutes.ToString(CultureInfo.InvariantCulture);
            MaxSamplesPerSignalText = options.MaxSamplesPerSignal.ToString(CultureInfo.InvariantCulture);
            MaxRenderedPointsPerChartText = options.MaxRenderedPointsPerChart.ToString(CultureInfo.InvariantCulture);

            PauseDashboardWhenHidden = options.PauseDashboardWhenHidden;
            LimitDetachedChartsSameAsDashboard = options.LimitDetachedChartsSameAsDashboard;
            DisableLineSmoothnessWhenHeavy = options.DisableLineSmoothnessWhenHeavy;
        }
        finally
        {
            _isLoadingPerformanceFields = false;
        }
    }

    partial void OnExportLongCsvChanged(bool value)
    {
        SaveExportOptions();
    }

    partial void OnExportWideCsvChanged(bool value)
    {
        SaveExportOptions();
    }

    partial void OnExportRawFramesCsvChanged(bool value)
    {
        SaveExportOptions();
    }

    private void SaveExportOptions()
    {
        if (_isLoadingExportOptions)
            return;

        _exportSettingsStore.SaveOptions(new TelemetryExportOptions
        {
            ExportLongCsv = ExportLongCsv,
            ExportWideCsv = ExportWideCsv,
            ExportRawFramesCsv = ExportRawFramesCsv
        });
    }

    partial void OnSelectedPerformanceProfileChanged(string value)
    {
        if (_isLoadingPerformanceFields)
            return;

        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value == TelemetryPerformanceOptions.CustomProfile)
        {
            RefreshPerformancePreview();
            return;
        }

        var profileOptions = TelemetryPerformanceOptions.CreateForProfile(value);
        LoadPerformanceFields(profileOptions);

        PerformanceStatusText = $"Wybrano profil „{value}”. Kliknij „Zastosuj ustawienia”, żeby go aktywować.";
        RefreshPerformancePreview();
    }

    partial void OnDashboardFpsTextChanged(string value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnMinSampleIntervalMsTextChanged(string value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnDashboardHistoryMinutesTextChanged(string value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnMaxSamplesPerSignalTextChanged(string value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnMaxRenderedPointsPerChartTextChanged(string value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnPauseDashboardWhenHiddenChanged(bool value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnLimitDetachedChartsSameAsDashboardChanged(bool value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    partial void OnDisableLineSmoothnessWhenHeavyChanged(bool value)
    {
        MarkPerformanceAsCustomAndRefresh();
    }

    private void MarkPerformanceAsCustomAndRefresh()
    {
        if (_isLoadingPerformanceFields)
            return;

        if (SelectedPerformanceProfile != TelemetryPerformanceOptions.CustomProfile)
        {
            _isLoadingPerformanceFields = true;
            SelectedPerformanceProfile = TelemetryPerformanceOptions.CustomProfile;
            _isLoadingPerformanceFields = false;
        }

        RefreshPerformancePreview();
    }

    [RelayCommand]
    private async Task ApplyPerformanceSettings()
    {
        var options = BuildPendingPerformanceOptions();
        options.Normalize();

        var snapshot = BuildPerformanceSnapshot(options);

        var isHeavy =
            snapshot.EstimatedRenderCost is "Wysoki" or "Bardzo wysoki" ||
            snapshot.EstimatedMemoryCost is "Wysoki" or "Bardzo wysoki";

        if (isHeavy)
        {
            var confirm = await MainPage.DisplayAlert(
                "Wysoki koszt ustawień",
                $"Te ustawienia mogą powodować przycięcia przy wielu wykresach.\n\n{snapshot.EstimatedCostText}\n\nCzy zastosować mimo to?",
                "Zastosuj",
                "Anuluj");

            if (!confirm)
                return;
        }

        _performanceSettingsStore.SaveOptions(options);
        LoadPerformanceFields(options);

        PerformanceStatusText = $"Zastosowano ustawienia: {options.BuildSummary()}";
        RefreshPerformancePreview();
    }

    [RelayCommand]
    private void RefreshPerformanceMeasurement()
    {
        RefreshPerformancePreview();
        PerformanceStatusText = "Odświeżono pomiar CPU/RAM i estymację kosztu.";
    }

    [RelayCommand]
    private async Task ClearDashboardHistory()
    {
        var confirm = await MainPage.DisplayAlert(
            "Wyczyścić historię dashboardu?",
            "Zostaną usunięte tylko punkty podglądu dashboardu. Wybór sygnałów na wykresach zostanie zachowany. Pełny zapis .rfdlog nie zostanie naruszony.",
            "Wyczyść",
            "Anuluj");

        if (!confirm)
            return;

        _sessionDetailViewModel.ClearDashboardPreviewHistory();

        PerformanceStatusText = "Wyczyszczono historię podglądu dashboardu. Dane .rfdlog zostały zachowane.";
        RefreshPerformancePreview();
    }

    private void RefreshPerformancePreview()
    {
        var options = BuildPendingPerformanceOptions();
        options.Normalize();

        var snapshot = BuildPerformanceSnapshot(options);

        PerformanceCurrentUsageText =
            $"CPU aplikacji: {snapshot.CpuText}\n" +
            $"RAM aplikacji: {snapshot.RamText}\n" +
            $"Wykresy: {snapshot.ChartCountText}\n" +
            $"Punkty w historii dashboardu: {snapshot.HistoryPointCountText}\n" +
            $"Śledzone sygnały: {snapshot.TrackedSignalCountText}";

        PerformanceEstimateText =
            $"Realna historia dashboardu przy tych limitach: {snapshot.EffectiveHistoryText}\n" +
            $"Szacowany RAM historii dashboardu: {snapshot.EstimatedDashboardMemoryText}\n" +
            $"Szacowany koszt: {snapshot.EstimatedCostText}";

        PerformanceRecommendationText = snapshot.Recommendation;
    }

    private TelemetryPerformanceSnapshot BuildPerformanceSnapshot(TelemetryPerformanceOptions options)
    {
        return _performanceMonitor.GetSnapshot(
            options,
            activeChartCount: _sessionDetailViewModel.VisibleCharts.Count,
            detachedChartCount: _sessionDetailViewModel.DetachedCharts.Count,
            dashboardHistoryPointCount: _sessionDetailViewModel.GetDashboardHistoryPointCount(),
            trackedSignalCount: Math.Max(1, _sessionDetailViewModel.GetTrackedSignalCount()));
    }

    private TelemetryPerformanceOptions BuildPendingPerformanceOptions()
    {
        var current = _performanceSettingsStore.GetOptions();

        var options = new TelemetryPerformanceOptions
        {
            Profile = string.IsNullOrWhiteSpace(SelectedPerformanceProfile)
                ? TelemetryPerformanceOptions.CustomProfile
                : SelectedPerformanceProfile,

            DashboardFps = ParseInt(DashboardFpsText, current.DashboardFps),
            MinSampleIntervalMs = ParseInt(MinSampleIntervalMsText, current.MinSampleIntervalMs),
            DashboardHistoryMinutes = ParseInt(DashboardHistoryMinutesText, current.DashboardHistoryMinutes),
            MaxSamplesPerSignal = ParseInt(MaxSamplesPerSignalText, current.MaxSamplesPerSignal),
            MaxRenderedPointsPerChart = ParseInt(MaxRenderedPointsPerChartText, current.MaxRenderedPointsPerChart),

            PauseDashboardWhenHidden = PauseDashboardWhenHidden,
            LimitDetachedChartsSameAsDashboard = LimitDetachedChartsSameAsDashboard,
            DisableLineSmoothnessWhenHeavy = DisableLineSmoothnessWhenHeavy
        };

        options.Normalize();

        return options;
    }

    private static int ParseInt(string? text, int fallback)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            return value;

        return fallback;
    }

    private void OnDriversCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (Driver driver in e.NewItems)
                driver.PropertyChanged += OnDriverPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (Driver driver in e.OldItems)
                driver.PropertyChanged -= OnDriverPropertyChanged;
        }
    }

    private void OnDriverPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Driver.Name))
            SaveDriversToPreferences();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        CurrentTab = tabName;

        if (tabName == "Performance")
            RefreshPerformancePreview();
    }

    [RelayCommand]
    private void UseLightTheme()
    {
        ApplyTheme("Jasny", save: true);
    }

    [RelayCommand]
    private void UseDarkTheme()
    {
        ApplyTheme("Ciemny", save: true);
    }

    [RelayCommand]
    private void AddDriver()
    {
        var name = NewDriverName?.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return;

        Drivers.Add(new Driver { Name = name });
        NewDriverName = string.Empty;

        SaveDriversToPreferences();
    }

    [RelayCommand]
    private void DeleteDriver(Driver? driver)
    {
        if (driver is null)
            return;

        Drivers.Remove(driver);
        SaveDriversToPreferences();
    }

    private void LoadDriversFromPreferences()
    {
        try
        {
            var json = Preferences.Get(DriversPreferenceKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
                return;

            var drivers = JsonSerializer.Deserialize<List<Driver>>(json);

            if (drivers is null)
                return;

            Drivers.Clear();

            foreach (var driver in drivers)
                Drivers.Add(driver);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd wczytywania kierowców: {ex.Message}");
        }
    }

    private void SaveDriversToPreferences()
    {
        try
        {
            var json = JsonSerializer.Serialize(Drivers.ToList());
            Preferences.Set(DriversPreferenceKey, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Błąd zapisu kierowców: {ex.Message}");
        }
    }

    private void ApplyTheme(string themeName, bool save)
    {
        SelectedTheme = themeName == "Jasny" ? "Jasny" : "Ciemny";

        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = SelectedTheme == "Jasny"
                ? AppTheme.Light
                : AppTheme.Dark;
        }

        if (save)
            Preferences.Set(ThemePreferenceKey, SelectedTheme);
    }

    private static Page MainPage =>
        Application.Current?.MainPage
        ?? throw new InvalidOperationException("Brak aktywnej strony aplikacji.");
}

public partial class Driver : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;
}