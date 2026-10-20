using System.Text.Json;

namespace TelemetryApp.Backend.Decoding;

public static class CanDefinitionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<CanDefinitionSet> LoadFromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await LoadFromStreamAsync(stream, cancellationToken);
    }

    public static async Task<CanDefinitionSet> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var definitionFile = await JsonSerializer.DeserializeAsync<CanDefinitionFile>(stream, Options, cancellationToken)
                             ?? throw new InvalidOperationException("Nie udało się zdeserializować JSON-a definicji CAN.");

        if (definitionFile.Frames.Count == 0)
            throw new InvalidOperationException("JSON definicji CAN nie zawiera żadnych ramek.");

        return new CanDefinitionSet(definitionFile);
    }
}
