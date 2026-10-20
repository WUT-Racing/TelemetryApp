using System.Text.Json.Serialization;

namespace TelemetryApp.Backend.Sessions;

public sealed class TelemetrySession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsTest { get; set; }
    public DateTime StartedAtLocal { get; set; } = DateTime.Now;
    public DateTime? EndedAtLocal { get; set; }
    public string DirectoryPath { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public List<TelemetrySessionRun> Runs { get; set; } = new();

    [JsonIgnore]
    public bool IsEnded => EndedAtLocal.HasValue;

    [JsonIgnore]
    public bool HasCompletedRun => Runs.Any(run => run.StoppedAtLocal.HasValue);

    [JsonIgnore]
    public string DisplayName => IsTest ? $"{Name} (test)" : Name;

    [JsonIgnore]
    public string TimeText => $"{StartedAtLocal:yyyy-MM-dd HH:mm}" + (EndedAtLocal is null ? string.Empty : $" – {EndedAtLocal:HH:mm}");

    [JsonIgnore]
    public string StatusText => IsEnded ? "Zakończona" : "Aktywna";

    [JsonIgnore]
    public string SourceText => Runs.Count == 0
        ? "Brak połączeń"
        : string.Join(", ", Runs.Select(run => run.Source).Distinct(StringComparer.OrdinalIgnoreCase));
}

public sealed class TelemetrySessionRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = string.Empty;
    public DateTime StartedAtLocal { get; set; } = DateTime.Now;
    public DateTime? StoppedAtLocal { get; set; }
    public string RawLogPath { get; set; } = string.Empty;
}
