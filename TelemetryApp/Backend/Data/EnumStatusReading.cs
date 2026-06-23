using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace TelemetryApp.Frontend.Data;

public partial class EnumStatusReading : ObservableObject
{
    [ObservableProperty]
    private int rowIndex;

    [ObservableProperty]
    private string statusName = string.Empty;

    [ObservableProperty]
    private bool isPinned;

    [ObservableProperty]
    private bool isEnabled = false;

    [ObservableProperty]
    private string colorHex = "#AB47BC";

    [ObservableProperty]
    private SensorColorOption? selectedColorOption = SensorColorOption.FindByHex("#AB47BC");

    [ObservableProperty]
    private string currentText = "—";

    [ObservableProperty]
    private string lastUpdateText = "Brak danych";

    public string SignalKey { get; set; } = string.Empty;
    public ushort CanId { get; set; }
    public string? FrameSummary { get; set; }
    public string OriginalSignalName { get; set; } = string.Empty;

    public string PinText => IsPinned ? "★" : "☆";
    public string PinButtonText => IsPinned ? "Odepnij" : "Przypnij";
    public string EnabledText => IsEnabled ? "Włączony" : "Wyłączony";
    public Color StatusColor => ParseColor(ColorHex);

    public string DetailsText
    {
        get
        {
            var frameText = string.IsNullOrWhiteSpace(FrameSummary) ? string.Empty : $" — {FrameSummary}";
            return $"0x{CanId:X3} / {SignalKey}{frameText}";
        }
    }

    public string SearchText => $"{StatusName} {OriginalSignalName} {SignalKey} {FrameSummary} 0x{CanId:X3} {CurrentText}";

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(PinText));
        OnPropertyChanged(nameof(PinButtonText));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EnabledText));
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnColorHexChanged(string value)
    {
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnSelectedColorOptionChanged(SensorColorOption? value)
    {
        if (value is not null)
            ColorHex = value.Hex;
    }

    partial void OnStatusNameChanged(string value)
    {
        OnPropertyChanged(nameof(SearchText));
    }

    partial void OnCurrentTextChanged(string value)
    {
        OnPropertyChanged(nameof(SearchText));
    }

    public void SetColor(SensorColorOption? option)
    {
        if (option is null)
            return;

        ColorHex = option.Hex;
    }

    public void ClearValue()
    {
        CurrentText = "—";
        LastUpdateText = "Brak danych";
    }

    public void UpdateFromSample(TelemetryApp.Frontend.ViewModels.SignalSample sample)
    {
        CurrentText = string.IsNullOrWhiteSpace(sample.DisplayValue)
            ? sample.Value.ToString("0.###")
            : sample.DisplayValue;

        LastUpdateText = FormatTimestamp(sample.TimestampMs);
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

    private static Color ParseColor(string? hex)
    {
        try
        {
            return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#AB47BC" : hex);
        }
        catch
        {
            return Color.FromArgb("#AB47BC");
        }
    }
}
