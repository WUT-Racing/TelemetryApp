using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelemetryApp.Backend.Decoding;

public sealed class CanDefinitionFile
{
    [JsonPropertyName("metadata")]
    public CanDefinitionMetadata? Metadata { get; set; }

    [JsonPropertyName("frames")]
    public List<CanFrameDefinition> Frames { get; set; } = new();
}

public sealed class CanDefinitionMetadata
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("default_dlc")]
    public int DefaultDlc { get; set; } = 8;
}

public sealed class CanFrameDefinition
{
    [JsonPropertyName("id")]
    public ushort Id { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("frequency_hz")]
    public double? FrequencyHz { get; set; }

    [JsonPropertyName("dlc")]
    public int Dlc { get; set; } = 8;

    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    [JsonPropertyName("signals")]
    public List<CanSignalDefinition> Signals { get; set; } = new();
}

public sealed class CanSignalDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("byte_start")]
    public int ByteStart { get; set; }

    [JsonPropertyName("byte_length")]
    public int ByteLength { get; set; }

    [JsonPropertyName("bit_start")]
    public int BitStart { get; set; }

    [JsonPropertyName("bit_length")]
    public int BitLength { get; set; }

    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = "unsigned";

    [JsonPropertyName("factor")]
    public double Factor { get; set; } = 1.0;

    [JsonPropertyName("offset")]
    public double Offset { get; set; } = 0.0;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("enum")]
    public Dictionary<string, JsonElement>? Enum { get; set; }

    [JsonPropertyName("reserved")]
    public bool Reserved { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("safe_min")]
    public double? SafeMin { get; set; }

    [JsonPropertyName("safe_max")]
    public double? SafeMax { get; set; }
}
