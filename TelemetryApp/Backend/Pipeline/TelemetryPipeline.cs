using System.Threading.Channels;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Protocol;
using TelemetryApp.Backend.Storage;

namespace TelemetryApp.Backend.Pipeline;

public sealed class TelemetryPipeline : IAsyncDisposable
{
    private readonly TelemetryBlockParser _parser = new();
    private readonly CanSignalDecoder? _decoder;
    private readonly RawTelemetryLogWriter _logWriter;
    private readonly CancellationTokenSource _internalCts = new();

    private readonly Channel<TelemetryBlock> _logChannel;
    private readonly Channel<TelemetryBlock> _decodeChannel;

    private Task? _readerTask;
    private Task? _loggerTask;
    private Task? _decoderTask;

    private long _bytesReceived;
    private long _blocksReceived;
    private long _recordsReceived;
    private long _signalsDecoded;
    private long _crcErrors;
    private long _parseErrors;
    private long _lostBlocks;
    private uint? _lastSequence;
    private ulong? _lastTimestamp;

    public event Action<TelemetryPipelineStats>? StatsChanged;
    public event Action<TelemetryBlock>? BlockReceived;
    public event Action<DecodedSignalBatch>? SignalsDecoded;
    public event Action<Exception>? PipelineFaulted;

    public string LogFilePath => _logWriter.FilePath;

    public TelemetryPipeline(string logFilePath, CanSignalDecoder? decoder = null)
    {
        _decoder = decoder;
        _logWriter = new RawTelemetryLogWriter(logFilePath);

        _logChannel = Channel.CreateBounded<TelemetryBlock>(new BoundedChannelOptions(512)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _decodeChannel = Channel.CreateBounded<TelemetryBlock>(new BoundedChannelOptions(128)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public Task StartAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, cancellationToken);
        var ct = linkedCts.Token;

        _readerTask = Task.Run(() => ReaderLoopAsync(inputStream, ct), CancellationToken.None);
        _loggerTask = Task.Run(() => LoggerLoopAsync(ct), CancellationToken.None);
        _decoderTask = Task.Run(() => DecoderLoopAsync(ct), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _internalCts.Cancel();

        try
        {
            var tasks = new[] { _readerTask, _loggerTask, _decoderTask }.Where(t => t is not null).Cast<Task>().ToArray();
            if (tasks.Length > 0)
                await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Normalne przy zamykaniu.
        }
        finally
        {
            await _logWriter.FlushAsync(CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _logWriter.DisposeAsync();
        _internalCts.Dispose();
    }

    private async Task ReaderLoopAsync(Stream inputStream, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await inputStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (read <= 0)
                    continue;

                Interlocked.Add(ref _bytesReceived, read);
                var results = _parser.Push(buffer.AsSpan(0, read));

                foreach (var result in results)
                {
                    if (!result.IsValid)
                    {
                        if (result.Error?.Code == "CRC_MISMATCH")
                            Interlocked.Increment(ref _crcErrors);
                        else
                            Interlocked.Increment(ref _parseErrors);

                        PublishStats();
                        continue;
                    }

                    var block = result.Block!;
                    UpdateSequenceStats(block);

                    Interlocked.Increment(ref _blocksReceived);
                    Interlocked.Add(ref _recordsReceived, block.Count);
                    _lastTimestamp = block.Timestamp;

                    await _logChannel.Writer.WriteAsync(block, cancellationToken);
                    await _decodeChannel.Writer.WriteAsync(block, cancellationToken);

                    BlockReceived?.Invoke(block);
                    PublishStats();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normalne przy zamykaniu.
        }
        catch (Exception ex)
        {
            PipelineFaulted?.Invoke(ex);
            throw;
        }
        finally
        {
            _logChannel.Writer.TryComplete();
            _decodeChannel.Writer.TryComplete();
        }
    }

    private async Task LoggerLoopAsync(CancellationToken cancellationToken)
    {
        var blocksSinceFlush = 0;

        await foreach (var block in _logChannel.Reader.ReadAllAsync(cancellationToken))
        {
            await _logWriter.WriteBlockAsync(block, cancellationToken);
            blocksSinceFlush++;

            // 10 bloków ≈ 1 s przy bloku co 100 ms.
            if (blocksSinceFlush >= 10)
            {
                await _logWriter.FlushAsync(cancellationToken);
                blocksSinceFlush = 0;
            }
        }
    }

    private async Task DecoderLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var block in _decodeChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (_decoder is null)
                continue;

            var decodedBatch = _decoder.Decode(block);
            Interlocked.Add(ref _signalsDecoded, decodedBatch.Count);
            SignalsDecoded?.Invoke(decodedBatch);
        }
    }

    private void UpdateSequenceStats(TelemetryBlock block)
    {
        if (_lastSequence.HasValue)
        {
            var expected = _lastSequence.Value + 1;
            if (block.Sequence != expected)
            {
                var lost = block.Sequence > expected
                    ? block.Sequence - expected
                    : 1;

                Interlocked.Add(ref _lostBlocks, lost);
            }
        }

        _lastSequence = block.Sequence;
    }

    private void PublishStats()
    {
        StatsChanged?.Invoke(new TelemetryPipelineStats
        {
            BytesReceived = Interlocked.Read(ref _bytesReceived),
            BlocksReceived = Interlocked.Read(ref _blocksReceived),
            RecordsReceived = Interlocked.Read(ref _recordsReceived),
            SignalsDecoded = Interlocked.Read(ref _signalsDecoded),
            CrcErrors = Interlocked.Read(ref _crcErrors),
            ParseErrors = Interlocked.Read(ref _parseErrors),
            LostBlocks = Interlocked.Read(ref _lostBlocks),
            LastSequence = _lastSequence,
            LastTimestamp = _lastTimestamp,
            LogFilePath = LogFilePath
        });
    }
}
