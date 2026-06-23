using System.Buffers.Binary;
using System.Text;
using TelemetryApp.Backend.Protocol;

namespace TelemetryApp.Backend.Storage;

public sealed class RawTelemetryLogReader : IAsyncDisposable
{
    private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("RFDLOG01");

    private readonly FileStream _stream;
    private readonly TelemetryBlockParser _parser = new();
    private bool _disposed;

    public string FilePath { get; }
    public int LogVersion { get; private set; }
    public long CreatedAtUnixMs { get; private set; }
    public int RecordSize { get; private set; }

    private RawTelemetryLogReader(string filePath)
    {
        FilePath = filePath;
        _stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public static async Task<RawTelemetryLogReader> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var reader = new RawTelemetryLogReader(filePath);
        await reader.ReadHeaderAsync(cancellationToken);
        return reader;
    }

    public async Task<TelemetryBlock?> ReadNextBlockAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var lengthBytes = new byte[4];
        var hasLength = await TryReadExactlyAsync(lengthBytes, cancellationToken);
        if (!hasLength)
            return null;

        var blockLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (blockLength <= 0 || blockLength > TelemetryProtocolConstants.MaxBlockSize)
            throw new InvalidDataException($"Niepoprawna długość bloku w logu: {blockLength}.");

        var rawBlock = new byte[blockLength];
        await ReadExactlyOrThrowAsync(rawBlock, cancellationToken);

        var results = _parser.Push(rawBlock);
        if (results.Count != 1)
            throw new InvalidDataException($"Parser zwrócił nieoczekiwaną liczbę wyników: {results.Count}.");

        var result = results[0];
        if (!result.IsValid)
            throw new InvalidDataException($"Niepoprawny blok w logu: {result.Error?.Code} {result.Error?.Message}");

        return result.Block;
    }

    public async IAsyncEnumerable<TelemetryBlock> ReadAllBlocksAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var block = await ReadNextBlockAsync(cancellationToken);
            if (block is null)
                yield break;

            yield return block;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _stream.DisposeAsync();
    }

    private async Task ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var magic = new byte[FileMagic.Length];
        await ReadExactlyOrThrowAsync(magic, cancellationToken);

        if (!magic.SequenceEqual(FileMagic))
            throw new InvalidDataException("To nie jest poprawny plik RFDLOG01.");

        var header = new byte[4 + 8 + 4];
        await ReadExactlyOrThrowAsync(header, cancellationToken);

        var offset = 0;
        LogVersion = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(offset, 4));
        offset += 4;

        CreatedAtUnixMs = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(offset, 8));
        offset += 8;

        RecordSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(offset, 4));

        if (RecordSize != TelemetryProtocolConstants.RecordSize)
            throw new InvalidDataException($"Nieobsługiwany rozmiar rekordu: {RecordSize}.");
    }

    private async Task<bool> TryReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                if (offset == 0)
                    return false;

                throw new EndOfStreamException("Nieoczekiwany koniec pliku .rfdlog.");
            }

            offset += read;
        }

        return true;
    }

    private async Task ReadExactlyOrThrowAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var ok = await TryReadExactlyAsync(buffer, cancellationToken);
        if (!ok)
            throw new EndOfStreamException("Nieoczekiwany koniec pliku .rfdlog.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RawTelemetryLogReader));
    }
}
