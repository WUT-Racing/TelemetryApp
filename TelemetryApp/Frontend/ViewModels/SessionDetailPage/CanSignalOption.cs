namespace TelemetryApp.Frontend.ViewModels;

public sealed class CanSignalOption
{
    public required ushort CanId { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Unit { get; init; }
    public string? FrameSummary { get; init; }
    public bool IsEnumLike { get; init; }

    public string DisplayName
    {
        get
        {
            var unitText = string.IsNullOrWhiteSpace(Unit) ? string.Empty : $" [{Unit}]";
            var typeText = IsEnumLike ? " [enum/status]" : string.Empty;
            return $"{Name}{unitText}{typeText} — 0x{CanId:X3}";
        }
    }

    public string SortText => string.IsNullOrWhiteSpace(Name) ? Key : Name;

    public bool Matches(string signalKey, ushort canId) =>
        CanId == canId && string.Equals(Key, signalKey, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => DisplayName;
}
