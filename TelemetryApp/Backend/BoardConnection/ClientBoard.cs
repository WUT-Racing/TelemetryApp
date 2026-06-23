using System.IO.Ports;
using Microsoft.Maui.Storage;
using TelemetryApp.Backend.Decoding;
using TelemetryApp.Backend.Pipeline;
using TelemetryApp.Backend.Sessions;
using TelemetryApp.Backend.Simulation;

namespace TelemetryApp.Backend.BoardConnection;

public sealed class ClientBoard : IAsyncDisposable
{
    private readonly TelemetrySessionManager _sessionManager;

    private SerialPort? _serialPort;
    private SimulatedTelemetryStream? _simulationStream;
    private TelemetryPipeline? _pipeline;
    private CancellationTokenSource? _cts;
    private bool _isConnected;

    public bool IsConnected => _isConnected;
    public TelemetrySession? ActiveSession => _sessionManager.ActiveSession;

    public event Action<string>? OnStatusChanged;
    public event Action<TelemetryPipelineStats>? OnStatsChanged;
    public event Action<DecodedSignalBatch>? OnSignalsDecoded;
    public event Action<Exception>? OnError;

    public ClientBoard(TelemetrySessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public async Task ConnectAsync(string portName, int baudRate = 460800, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Nazwa portu nie może być pusta.", nameof(portName));

        if (IsConnected)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var run = _sessionManager.StartRun("serial", markSessionAsTest: false);
        var definitions = await LoadDefaultDefinitionsAsync(_cts.Token);
        var decoder = new CanSignalDecoder(definitions);

        _pipeline = CreatePipeline(run.RawLogPath, decoder);

        _serialPort = new SerialPort(portName, baudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadBufferSize = 1024 * 1024,
            WriteBufferSize = 64 * 1024
        };

        try
        {
            _serialPort.Open();
            await _pipeline.StartAsync(_serialPort.BaseStream, _cts.Token);

            _isConnected = true;
            OnStatusChanged?.Invoke($"Połączono z {portName}, baud={baudRate}. Raw log: {run.RawLogPath}");
        }
        catch
        {
            _sessionManager.StopActiveRun();
            throw;
        }
    }

    public async Task ConnectSimulatorAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var run = _sessionManager.StartRun("simulator", markSessionAsTest: true);
        var definitions = await LoadDefaultDefinitionsAsync(_cts.Token);
        var decoder = new CanSignalDecoder(definitions);

        var knownIds = definitions.FramesById.Keys
            .Where(id => id <= 0x07FF)
            .Take(64)
            .ToArray();

        var options = new SimulationOptions
        {
            BlocksPerSecond = 10,
            RecordsPerBlock = 211,
            ProtocolVersion = 1,
            CanIds = knownIds
        };

        _simulationStream = new SimulatedTelemetryStream(options);
        _pipeline = CreatePipeline(run.RawLogPath, decoder);

        try
        {
            await _pipeline.StartAsync(_simulationStream, _cts.Token);

            _isConnected = true;
            OnStatusChanged?.Invoke($"Uruchomiono symulator STM: {options.BlocksPerSecond} bloków/s, {options.RecordsPerBlock} rekordów/blok. Raw log: {run.RawLogPath}");
        }
        catch
        {
            _sessionManager.StopActiveRun();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();

        // Zamykamy źródło danych przed oczekiwaniem na pipeline, żeby przerwać ewentualny ReadAsync.
        if (_serialPort is not null)
        {
            if (_serialPort.IsOpen)
                _serialPort.Close();

            _serialPort.Dispose();
            _serialPort = null;
        }

        if (_pipeline is not null)
        {
            await _pipeline.StopAsync();
            await _pipeline.DisposeAsync();
            _pipeline = null;
        }

        if (_simulationStream is not null)
        {
            _simulationStream.Dispose();
            _simulationStream = null;
        }

        _cts?.Dispose();
        _cts = null;
        _isConnected = false;
        _sessionManager.StopActiveRun();

        OnStatusChanged?.Invoke("Rozłączono. Sesja nadal trwa — możesz kontynuować albo zakończyć sesję.");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private TelemetryPipeline CreatePipeline(string logPath, CanSignalDecoder decoder)
    {
        var pipeline = new TelemetryPipeline(logPath, decoder);
        pipeline.StatsChanged += stats => OnStatsChanged?.Invoke(stats);
        pipeline.SignalsDecoded += batch => OnSignalsDecoded?.Invoke(batch);
        pipeline.PipelineFaulted += ex => OnError?.Invoke(ex);
        return pipeline;
    }

    private static async Task<CanDefinitionSet> LoadDefaultDefinitionsAsync(CancellationToken cancellationToken)
    {
        // sygnaly.json jest skopiowany do Resources/Raw, a csproj ma MauiAsset Include="Resources\\Raw\\**".
        await using var stream = await FileSystem.OpenAppPackageFileAsync("sygnaly.json");
        return await CanDefinitionLoader.LoadFromStreamAsync(stream, cancellationToken);
    }
}
