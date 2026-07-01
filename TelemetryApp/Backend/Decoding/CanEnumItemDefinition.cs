using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelemetryApp.Backend.Decoding;

[JsonConverter(typeof(CanEnumItemConverter))]
public sealed class CanEnumItemDefinition
{
    // Jeśli true to enum jest typu prostego
    public bool IsSimple { get; set; }
    public string? StringValue { get; set; }

    public string? Key { get; set; }
    public string? Label { get; set; }
    public string? Description { get; set; }
}

public class CanEnumItemConverter : JsonConverter<CanEnumItemDefinition>
{
    public override CanEnumItemDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Prosty string
        if (reader.TokenType == JsonTokenType.String)
        {
            return new CanEnumItemDefinition 
            { 
                IsSimple = true, 
                StringValue = reader.GetString() 
            };
        }

        // Złożony obiekt
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            
            return new CanEnumItemDefinition
            {
                IsSimple = false,
                Key = root.TryGetProperty("key", out var k) ? k.GetString() : null,
                Label = root.TryGetProperty("label", out var l) ? l.GetString() : null,
                Description = root.TryGetProperty("description", out var d) ? d.GetString() : null
            };
        }

        return new CanEnumItemDefinition(); 
    }

    public override void Write(Utf8JsonWriter writer, CanEnumItemDefinition value, JsonSerializerOptions options)
    {
        if (value.IsSimple)
        {
            writer.WriteStringValue(value.StringValue);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("key", value.Key);
            writer.WriteString("label", value.Label);
            writer.WriteString("description", value.Description);
            writer.WriteEndObject();
        }
    }
}