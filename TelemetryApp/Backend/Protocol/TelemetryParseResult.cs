namespace TelemetryApp.Backend.Protocol;

public sealed record TelemetryParseError(
    string Code,
    string Message,
    byte[]? RawBytes = null);

public sealed record TelemetryParseResult(
    TelemetryBlock? Block,
    TelemetryParseError? Error)
{
    public bool IsValid => Block is not null;

    public static TelemetryParseResult Valid(TelemetryBlock block) => new(block, null);

    public static TelemetryParseResult Invalid(string code, string message, byte[]? rawBytes = null) =>
        new(null, new TelemetryParseError(code, message, rawBytes));
}
