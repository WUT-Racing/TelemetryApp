namespace TelemetryApp.Backend.Simulation;

public sealed class SimulatedTelemetryStream : Stream
{
    private readonly TelemetryBlockGenerator _generator;
    private readonly SimulationOptions _options;
    private byte[]? _currentBlock;
    private int _currentOffset;
    private bool _disposed;

    public SimulatedTelemetryStream(SimulationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _generator = new TelemetryBlockGenerator(options);
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (buffer.Length == 0)
            return 0;

        if (_currentBlock is null || _currentOffset >= _currentBlock.Length)
        {
            await Task.Delay(_options.BlockPeriod, cancellationToken);
            _currentBlock = _generator.GenerateNextBlock();
            _currentOffset = 0;
        }

        var bytesToCopy = Math.Min(buffer.Length, _currentBlock.Length - _currentOffset);
        _currentBlock.AsMemory(_currentOffset, bytesToCopy).CopyTo(buffer);
        _currentOffset += bytesToCopy;

        return bytesToCopy;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }
}
