using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelemetryApp.Backend.Decoding;

public static class CanDefinitionSaver
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task SaveToFileAsync(CanDefinitionFile definition, string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, definition, Options, cancellationToken);
    }
}