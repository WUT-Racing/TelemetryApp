using System;
using System.Collections.Generic;
using System.Linq;

namespace TelemetryApp.Frontend.Data;

public sealed class SensorColorOption
{
    public required string Name { get; init; }
    public required string Hex { get; init; }

    public string DisplayName => $"{Name} ({Hex})";

    public override string ToString() => DisplayName;

    public static IReadOnlyList<SensorColorOption> Defaults { get; } = new List<SensorColorOption>
    {
        new() { Name = "Niebieski", Hex = "#42A5F5" },
        new() { Name = "Zielony", Hex = "#66BB6A" },
        new() { Name = "Pomarańczowy", Hex = "#FFA726" },
        new() { Name = "Czerwony", Hex = "#EF5350" },
        new() { Name = "Fioletowy", Hex = "#AB47BC" },
        new() { Name = "Turkusowy", Hex = "#26A69A" },
        new() { Name = "Żółty", Hex = "#FFCA28" },
        new() { Name = "Szary", Hex = "#9E9E9E" }
    };

    public static SensorColorOption FindByHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Defaults[0];

        return Defaults.FirstOrDefault(option => string.Equals(option.Hex, hex, StringComparison.OrdinalIgnoreCase))
            ?? Defaults[0];
    }
}
