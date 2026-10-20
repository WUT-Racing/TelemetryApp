using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelemetryApp.Backend.BoardConnection;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Sessions;
using TelemetryApp.Backend.UserSettings;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.Views;
using System.Diagnostics;
using TelemetryApp.Backend.Performance;

namespace TelemetryApp.Frontend.ViewModels
{
    public partial class SessionDetailViewModel : ObservableObject
    {
        private readonly ClientBoard _clientBoard;
        private readonly TelemetrySessionManager _sessionManager;
        private readonly SignalDisplaySettingsStore _displaySettingsStore;
        private readonly SignalHistoryStore _historyStore;
        private readonly TelemetryPerformanceSettingsStore _performanceSettingsStore;

        private TelemetryPerformanceOptions _performanceOptions = TelemetryPerformanceOptions.CreateDefault();
        private long _lastLiveUiUpdateTicks;
        private string? _currentSessionId;

        private readonly Dictionary<string, SensorReading> _sensorsBySignal = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EnumStatusReading> _enumStatusesBySignal = new(StringComparer.OrdinalIgnoreCase);

        private Window? _showTablesWindow;
        private Window? _sensorManagementWindow;
        private Window? _enumStatusWindow;
        private Window? _enumManagementWindow;

        private readonly Dictionary<SinglePlotViewModel, Window> _detachedPlotWindows = new();
        private readonly HashSet<Window> _detachedPlotWindowsClosingProgrammatically = new();

        public ObservableCollection<SinglePlotViewModel> VisibleCharts { get; } = new();

        public ObservableCollection<SinglePlotViewModel> DetachedCharts { get; } = new();

        public ObservableCollection<CanSignalOption> AvailableSignals { get; } = new();

        public ObservableCollection<CanSignalOption> AvailableEnumSignals { get; } = new();

        public ObservableCollection<SensorReading> Sensors { get; } = new();

        public ObservableCollection<EnumStatusReading> EnumStatuses { get; } = new();

        [ObservableProperty]
        private string dashboardStatus = "Ładowanie listy sygnałów z JSON-a...";

        public SessionDetailViewModel(
            ClientBoard clientBoard,
            TelemetrySessionManager sessionManager,
            SignalDisplaySettingsStore displaySettingsStore,
            TelemetryPerformanceSettingsStore performanceSettingsStore)
        {
            _clientBoard = clientBoard ?? throw new ArgumentNullException(nameof(clientBoard));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _displaySettingsStore = displaySettingsStore ?? throw new ArgumentNullException(nameof(displaySettingsStore));
            _performanceSettingsStore = performanceSettingsStore ?? throw new ArgumentNullException(nameof(performanceSettingsStore));
            _performanceOptions = _performanceSettingsStore.GetOptions();
            _historyStore = new SignalHistoryStore(_performanceOptions);
            _performanceSettingsStore.OptionsChanged += HandlePerformanceOptionsChanged;

            _clientBoard.OnSignalsDecoded += HandleSignalsDecoded;
            _sessionManager.ActiveSessionChanged += HandleActiveSessionChanged;

            _currentSessionId = _sessionManager.ActiveSession?.Id;

            _ = LoadAvailableSignalsAsync();
        }

        private IEnumerable<SinglePlotViewModel> GetAllChartViewModels()
        {
            return VisibleCharts
                .Concat(DetachedCharts)
                .Distinct()
                .ToList();
        }
        private bool ShouldUpdateLiveUi()
        {
            var fps = Math.Clamp(_performanceOptions.DashboardFps, 2, 30);
            var minIntervalMs = 1000.0 / fps;

            var now = Stopwatch.GetTimestamp();
            var last = Interlocked.Read(ref _lastLiveUiUpdateTicks);

            if (last != 0)
            {
                var elapsedMs = (now - last) * 1000.0 / Stopwatch.Frequency;

                if (elapsedMs < minIntervalMs)
                    return false;
            }

            Interlocked.Exchange(ref _lastLiveUiUpdateTicks, now);
            return true;
        }

        private void HandlePerformanceOptionsChanged(TelemetryPerformanceOptions options)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _performanceOptions = options.Clone();
                _performanceOptions.Normalize();

                _historyStore.ApplyOptions(_performanceOptions, pruneExisting: true);

                foreach (var plot in GetAllChartViewModels())
                    ReloadPlotHistory(plot);

                DashboardStatus = $"Zastosowano ustawienia wydajności: {_performanceOptions.BuildSummary()}";
            });
        }

        private void ReloadPlotHistory(SinglePlotViewModel plot)
        {
            var selected = plot.SelectedSignal;

            if (selected is null)
            {
                plot.LoadHistory(Array.Empty<SignalSample>());
                return;
            }

            var history = _historyStore.GetHistory(selected.CanId, selected.Key)
                .Where(sample => !sample.IsEnumLike)
                .ToList();

            plot.LoadHistory(history);
        }

        private async Task LoadAvailableSignalsAsync()
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync("sygnaly.json");
                var definitions = await CanDefinitionLoader.LoadFromStreamAsync(stream);

                var allOptions = definitions.Source.Frames
                    .SelectMany(frame => frame.Signals
                        .Where(signal =>
                            !signal.Reserved &&
                            !string.Equals(signal.DataType, "reserved", StringComparison.OrdinalIgnoreCase))
                        .Select(signal => new CanSignalOption
                        {
                            CanId = frame.Id,
                            Key = signal.Key,
                            Name = BuildFriendlySignalName(frame, signal),
                            Unit = signal.Unit,
                            FrameSummary = frame.Summary,
                            IsEnumLike = IsEnumLike(signal),
                            SafeMin = signal.SafeMin,
                            SafeMax = signal.SafeMax
                        }))
                    .OrderBy(option => option.SortText, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(option => option.CanId)
                    .ThenBy(option => option.Key, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var numericOptions = allOptions
                    .Where(option => !option.IsEnumLike)
                    .ToList();

                var enumOptions = allOptions
                    .Where(option => option.IsEnumLike)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableSignals.Clear();
                    AvailableEnumSignals.Clear();
                    Sensors.Clear();
                    EnumStatuses.Clear();
                    _sensorsBySignal.Clear();
                    _enumStatusesBySignal.Clear();

                    foreach (var option in numericOptions)
                        AvailableSignals.Add(option);

                    foreach (var option in enumOptions)
                        AvailableEnumSignals.Add(option);

                    foreach (var plot in GetAllChartViewModels())
                    {
                        plot.SetAvailableSignals(AvailableSignals);
                    }

                    var colorOptions = SensorColorOption.Defaults;

                    for (var i = 0; i < numericOptions.Count; i++)
                    {
                        var option = numericOptions[i];
                        var color = colorOptions[i % colorOptions.Count];

                        var sensor = new SensorReading
                        {
                            SensorId = i + 1,
                            RowIndex = i,
                            SensorName = option.Name,
                            OriginalSignalName = option.Name,
                            SignalKey = option.Key,
                            CanId = option.CanId,
                            Unit = option.Unit,
                            FrameSummary = option.FrameSummary,
                            ColorHex = color.Hex,
                            SelectedColorOption = color,
                            DisplayOrder = i,
                            SafeMin = option.SafeMin,
                            SafeMax = option.SafeMax
                        };

                        _displaySettingsStore.ApplyToSensor(sensor);

                        sensor.PropertyChanged += HandleSensorSettingsChanged;

                        Sensors.Add(sensor);
                        _sensorsBySignal[MakeSignalKey(option.CanId, option.Key)] = sensor;
                    }

                    for (var i = 0; i < enumOptions.Count; i++)
                    {
                        var option = enumOptions[i];
                        var color = colorOptions[(i + 4) % colorOptions.Count];

                        var status = new EnumStatusReading
                        {
                            RowIndex = i,
                            StatusName = option.Name,
                            OriginalSignalName = option.Name,
                            SignalKey = option.Key,
                            CanId = option.CanId,
                            FrameSummary = option.FrameSummary,
                            ColorHex = color.Hex,
                            SelectedColorOption = color,
                            DisplayOrder = i
                        };

                        _displaySettingsStore.ApplyToEnum(status);

                        status.PropertyChanged += HandleEnumSettingsChanged;

                        EnumStatuses.Add(status);
                        _enumStatusesBySignal[MakeSignalKey(option.CanId, option.Key)] = status;
                    }

                    DashboardStatus =
                        $"Sygnały liczbowe: {AvailableSignals.Count}, enum/status: {AvailableEnumSignals.Count}. Dashboard pokazuje wykresy; wartości liczbowe są w „Pokaż tabele”, a enumy w osobnym oknie.";
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DashboardStatus = $"Nie udało się wczytać sygnałów z JSON-a: {ex.Message}";
                });
            }
        }

        private void HandleActiveSessionChanged(TelemetrySession? session)
        {
            if (session is null)
            {
                DashboardStatus = "Sesja zakończona. Dane zostają w pamięci do eksportu, dopóki nie rozpoczniesz nowej sesji.";
                return;
            }


            if (session.Id == _currentSessionId)
                return;

            _currentSessionId = session.Id;
            ClearLiveData();
        }

        private void ClearLiveData()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _historyStore.Clear();

                foreach (var sensor in Sensors)
                    sensor.ClearValues();

                foreach (var status in EnumStatuses)
                    status.ClearValue();

                foreach (var plot in GetAllChartViewModels())
                    plot.LoadHistory(Array.Empty<SignalSample>());

                DashboardStatus = _currentSessionId is null
                    ? "Brak aktywnej sesji. Rozpocznij sesję i połączenie, żeby zbierać dane."
                    : "Rozpoczęto nową sesję — historia dashboardu została wyczyszczona.";
            });
        }

        public void ClearDashboardPreviewHistory()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _historyStore.Clear();

                foreach (var sensor in Sensors)
                    sensor.ClearValues();

                foreach (var status in EnumStatuses)
                    status.ClearValue();

                foreach (var plot in GetAllChartViewModels())
                    plot.LoadHistory(Array.Empty<SignalSample>());

                DashboardStatus = "Wyczyszczono historię podglądu dashboardu. Zapis .rfdlog nie został naruszony.";
            });
        }

        public int GetDashboardHistoryPointCount()
        {
            return _historyStore.GetTotalSampleCount();
        }

        public int GetTrackedSignalCount()
        {
            return Math.Max(
                Sensors.Count + EnumStatuses.Count,
                _historyStore.GetTrackedSignalCount());
        }

        private void HandleSensorSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is SensorReading sensor && IsPersistentSensorProperty(e.PropertyName))
                _displaySettingsStore.SaveSensor(sensor);
        }

        private void HandleEnumSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is EnumStatusReading status && IsPersistentEnumProperty(e.PropertyName))
                _displaySettingsStore.SaveEnum(status);
        }

        private static bool IsPersistentSensorProperty(string? propertyName)
        {
            return propertyName is nameof(SensorReading.SensorName)
                or nameof(SensorReading.IsEnabled)
                or nameof(SensorReading.DisplayOrder)
                or nameof(SensorReading.ColorHex);
        }

        private static bool IsPersistentEnumProperty(string? propertyName)
        {
            return propertyName is nameof(EnumStatusReading.StatusName)
                or nameof(EnumStatusReading.IsEnabled)
                or nameof(EnumStatusReading.DisplayOrder)
                or nameof(EnumStatusReading.ColorHex);
        }

        [RelayCommand]
        public void AddPlot()
        {
            var plot = new SinglePlotViewModel
            {
                Title = $"Wykres {VisibleCharts.Count + DetachedCharts.Count + 1}"
            };

            plot.SetAvailableSignals(AvailableSignals);

            plot.SelectedSignalChanged += HandlePlotSelectedSignalChanged;
            VisibleCharts.Add(plot);
        }

        [RelayCommand]
        public void RemovePlot(SinglePlotViewModel? plot)
        {
            if (plot is null)
                return;

            CloseDetachedPlotWindow(plot);

            plot.SelectedSignalChanged -= HandlePlotSelectedSignalChanged;

            if (VisibleCharts.Contains(plot))
                VisibleCharts.Remove(plot);

            if (DetachedCharts.Contains(plot))
                DetachedCharts.Remove(plot);

            DashboardStatus = $"Usunięto wykres: {plot.Title}";
        }

        [RelayCommand]
        private void DetachPlot(SinglePlotViewModel? plot)
        {
            if (plot is null)
                return;

            if (_detachedPlotWindows.TryGetValue(plot, out var existingWindow) &&
                IsWindowStillOpen(existingWindow))
            {
                BringWindowToFront(existingWindow);
                DashboardStatus = $"Wykres „{plot.Title}” jest już odpięty — przeniesiono okno na wierzch.";
                return;
            }

            if (VisibleCharts.Contains(plot))
                VisibleCharts.Remove(plot);

            if (!DetachedCharts.Contains(plot))
                DetachedCharts.Add(plot);

            OpenDetachedPlotWindow(plot);

            DashboardStatus = $"Odpięto wykres: {plot.Title}";
        }

        [RelayCommand]
        private void ShowDetachedPlot(SinglePlotViewModel? plot)
        {
            if (plot is null)
                return;

            if (_detachedPlotWindows.TryGetValue(plot, out var existingWindow) &&
                IsWindowStillOpen(existingWindow))
            {
                BringWindowToFront(existingWindow);
                DashboardStatus = $"Przeniesiono okno wykresu „{plot.Title}” na wierzch.";
                return;
            }

            OpenDetachedPlotWindow(plot);

            DashboardStatus = $"Pokazano odpięty wykres: {plot.Title}";
        }

        [RelayCommand]
        public void ReattachPlot(SinglePlotViewModel? plot)
        {
            if (plot is null)
                return;

            CloseDetachedPlotWindow(plot);

            if (DetachedCharts.Contains(plot))
                DetachedCharts.Remove(plot);

            if (!VisibleCharts.Contains(plot))
                VisibleCharts.Add(plot);

            DashboardStatus = $"Przypięto wykres z powrotem: {plot.Title}";
        }

        private void OpenDetachedPlotWindow(SinglePlotViewModel plot)
        {
            if (_detachedPlotWindows.TryGetValue(plot, out var existingWindow) &&
                IsWindowStillOpen(existingWindow))
            {
                BringWindowToFront(existingWindow);
                return;
            }

            var page = new DetachedPlotPage(plot, this);

            var window = new Window(page)
            {
                MinimumWidth = 900,
                MinimumHeight = 560,
                Title = plot.Title
            };

            PropertyChangedEventHandler titleHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(SinglePlotViewModel.Title))
                    window.Title = plot.Title;
            };

            plot.PropertyChanged += titleHandler;

            _detachedPlotWindows[plot] = window;

            window.Destroying += (_, _) =>
            {
                plot.PropertyChanged -= titleHandler;

                if (_detachedPlotWindows.TryGetValue(plot, out var registeredWindow) &&
                    ReferenceEquals(registeredWindow, window))
                {
                    _detachedPlotWindows.Remove(plot);
                }

                if (_detachedPlotWindowsClosingProgrammatically.Remove(window))
                    return;

                RemoveDetachedPlotAfterWindowClosed(plot);
            };

            Application.Current?.OpenWindow(window);
        }

        private void RemoveDetachedPlotAfterWindowClosed(SinglePlotViewModel plot)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                plot.SelectedSignalChanged -= HandlePlotSelectedSignalChanged;

                if (DetachedCharts.Contains(plot))
                    DetachedCharts.Remove(plot);

                if (VisibleCharts.Contains(plot))
                    VisibleCharts.Remove(plot);

                DashboardStatus = $"Zamknięto i usunięto wykres: {plot.Title}";
            });
        }

        private void CloseDetachedPlotWindow(SinglePlotViewModel plot)
        {
            if (!_detachedPlotWindows.TryGetValue(plot, out var window))
                return;

            _detachedPlotWindowsClosingProgrammatically.Add(window);

            try
            {
                Application.Current?.CloseWindow(window);
            }
            catch
            {
                _detachedPlotWindowsClosingProgrammatically.Remove(window);
            }

            _detachedPlotWindows.Remove(plot);
        }

        private void HandlePlotSelectedSignalChanged(SinglePlotViewModel plot)
        {
            ReloadPlotHistory(plot);
        }

        private void HandleSignalsDecoded(DecodedSignalBatch batch)
        {
            if (batch.Signals.Length == 0)
                return;

            var appendedSamples = _historyStore.AppendBatch(batch);

            if (appendedSamples.Count == 0)
                return;

            if (!ShouldUpdateLiveUi())
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var sample in appendedSamples)
                {
                    if (sample.IsEnumLike)
                    {
                        if (_enumStatusesBySignal.TryGetValue(MakeSignalKey(sample.CanId, sample.Key), out var status))
                            status.UpdateFromSample(sample);
                    }
                    else
                    {
                        foreach (var plot in GetAllChartViewModels())
                        {
                            if (plot.SelectedSignal is not null &&
                                plot.SelectedSignal.Matches(sample.Key, sample.CanId))
                            {
                                plot.AddSample(sample);
                            }
                        }

                        if (_sensorsBySignal.TryGetValue(MakeSignalKey(sample.CanId, sample.Key), out var sensor))
                            sensor.AddValue(sample.Value, ToDateTime(sample.TimestampMs));
                    }
                }

                DashboardStatus = $"Ostatnia paczka: seq={batch.BlockSequence}, sygnałów={batch.Signals.Length}, dane live zaktualizowane.";
            });
        }

        [RelayCommand]
        private void ShowTables()
        {
            if (IsWindowStillOpen(_showTablesWindow))
            {
                BringWindowToFront(_showTablesWindow);
                DashboardStatus = "Okno „Tabela sensorów” jest już otwarte — przeniesiono je na wierzch.";
                return;
            }

            var window = new Window(new ShowTables(Sensors.ToList()))
            {
                MinimumWidth = 900,
                MinimumHeight = 500,
                Title = "Tabela sensorów"
            };

            _showTablesWindow = window;

            window.Destroying += (_, _) =>
            {
                if (ReferenceEquals(_showTablesWindow, window))
                    _showTablesWindow = null;
            };

            Application.Current?.OpenWindow(window);
        }

        [RelayCommand]
        private void ManageSensors()
        {
            if (IsWindowStillOpen(_sensorManagementWindow))
            {
                BringWindowToFront(_sensorManagementWindow);
                DashboardStatus = "Okno „Zarządzaj sensorami” jest już otwarte — przeniesiono je na wierzch.";
                return;
            }

            var window = new Window(new SensorManagementPage(Sensors.ToList()))
            {
                MinimumWidth = 950,
                MinimumHeight = 520,
                Title = "Zarządzaj sensorami"
            };

            _sensorManagementWindow = window;

            window.Destroying += (_, _) =>
            {
                if (ReferenceEquals(_sensorManagementWindow, window))
                    _sensorManagementWindow = null;
            };

            Application.Current?.OpenWindow(window);
        }

        [RelayCommand]
        private void ShowEnums()
        {
            if (IsWindowStillOpen(_enumStatusWindow))
            {
                BringWindowToFront(_enumStatusWindow);
                DashboardStatus = "Okno „Enumy i statusy” jest już otwarte — przeniesiono je na wierzch.";
                return;
            }

            var window = new Window(new EnumStatusPage(EnumStatuses.ToList()))
            {
                MinimumWidth = 850,
                MinimumHeight = 500,
                Title = "Enumy i statusy"
            };

            _enumStatusWindow = window;

            window.Destroying += (_, _) =>
            {
                if (ReferenceEquals(_enumStatusWindow, window))
                    _enumStatusWindow = null;
            };

            Application.Current?.OpenWindow(window);
        }

        [RelayCommand]
        private void ManageEnums()
        {
            if (IsWindowStillOpen(_enumManagementWindow))
            {
                BringWindowToFront(_enumManagementWindow);
                DashboardStatus = "Okno „Zarządzaj enumami” jest już otwarte — przeniesiono je na wierzch.";
                return;
            }

            var window = new Window(new EnumManagementPage(EnumStatuses.ToList()))
            {
                MinimumWidth = 950,
                MinimumHeight = 520,
                Title = "Zarządzaj enumami"
            };

            _enumManagementWindow = window;

            window.Destroying += (_, _) =>
            {
                if (ReferenceEquals(_enumManagementWindow, window))
                    _enumManagementWindow = null;
            };

            Application.Current?.OpenWindow(window);
        }

        private static bool IsWindowStillOpen(Window? window)
        {
            var app = Application.Current;

            return window is not null &&
                   app is not null &&
                   app.Windows.Contains(window);
        }

        private static void BringWindowToFront(Window? window)
        {
            if (window is null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
#if WINDOWS
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    nativeWindow.Activate();
                }
#elif MACCATALYST || IOS
                if (window.Handler?.PlatformView is UIKit.UIWindow nativeWindow)
                {
                    nativeWindow.MakeKeyAndVisible();
                }
#endif
            });
        }


        private static bool IsEnumLike(CanSignalDefinition signal)
        {
            if (string.Equals(signal.DataType, "enum", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(signal.DataType, "bitfield", StringComparison.OrdinalIgnoreCase))
                return true;

            return signal.Enum is { Count: > 0 };
        }

        private static string BuildFriendlySignalName(
            CanFrameDefinition frame,
            CanSignalDefinition signal)
        {
            var signalName = FirstNonEmpty(signal.Name, HumanizeKey(signal.Key), "Sygnał");
            var frameName = CleanName(frame.Summary);

            if (string.IsNullOrWhiteSpace(frameName))
                return signalName;

            if (signalName.Contains(frameName, StringComparison.OrdinalIgnoreCase))
                return signalName;

            return $"{frameName} — {signalName}";
        }

        private static string HumanizeKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var text = key.Replace('_', ' ').Replace('-', ' ');
            text = Regex.Replace(text, "\\s+", " ").Trim();

            if (text.Length == 0)
                return string.Empty;

            return char.ToUpper(text[0]) + text[1..];
        }

        private static string CleanName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Regex.Replace(value.Trim(), "\\s+", " ");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return CleanName(value);
            }

            return string.Empty;
        }

        private static string MakeSignalKey(ushort canId, string key)
        {
            return $"{canId:X3}|{key}";
        }

        private static DateTime ToDateTime(ulong timestampMs)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unchecked((long)timestampMs))
                    .ToLocalTime()
                    .DateTime;
            }
            catch
            {
                return DateTime.Now;
            }
        }
    }
}