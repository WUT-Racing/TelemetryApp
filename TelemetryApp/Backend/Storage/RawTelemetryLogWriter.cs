using System.Buffers.Binary;
using System.Text;
using TelemetryApp.Backend.Protocol;

namespace TelemetryApp.Backend.Storage;

public sealed class RawTelemetryLogWriter : IAsyncDisposable
{
    private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("RFDLOG01");
    private readonly FileStream _stream;
    private bool _disposed;

    public string FilePath { get; }

    public RawTelemetryLogWriter(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Ścieżka logu nie może być pusta.", nameof(filePath));

        FilePath = filePath;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        WriteHeader();
    }

    public async ValueTask WriteBlockAsync(TelemetryBlock block, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, block.RawBytes.Length);

        await _stream.WriteAsync(lengthBytes.ToArray(), cancellationToken);
        await _stream.WriteAsync(block.RawBytes, cancellationToken);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _stream.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _stream.FlushAsync();
        await _stream.DisposeAsync();
    }

    private void WriteHeader()
    {
        _stream.Write(FileMagic);

        Span<byte> buffer = stackalloc byte[4 + 8 + 4];
        var offset = 0;

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, 4), 1); // log format version
        offset += 4;

        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset, 8), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        offset += 8;

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, 4), TelemetryProtocolConstants.RecordSize);

        _stream.Write(buffer);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RawTelemetryLogWriter));
    }
}
