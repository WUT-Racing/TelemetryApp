namespace TelemetryApp.Backend.Decoding;

public sealed class CanDefinitionSet
{
    private readonly Dictionary<ushort, CanFrameDefinition> _framesById;

    public CanDefinitionFile Source { get; }

    public IReadOnlyDictionary<ushort, CanFrameDefinition> FramesById => _framesById;

    public CanDefinitionSet(CanDefinitionFile source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _framesById = source.Frames.ToDictionary(frame => frame.Id);
    }

    public bool TryGetFrame(ushort canId, out CanFrameDefinition frame) =>
        _framesById.TryGetValue(canId, out frame!);
}
