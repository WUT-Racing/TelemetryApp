using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Maui.Storage;

namespace TelemetryApp.Backend.Sessions;

public sealed class TelemetrySessionManager
{
    private readonly object _sync = new();
    private readonly string _sessionsRoot;
    private readonly string _indexPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private TelemetrySession? _activeSession;
    private TelemetrySessionRun? _activeRun;

    public ObservableCollection<TelemetrySession> Sessions { get; } = new();

    public TelemetrySession? ActiveSession
    {
        get
        {
            lock (_sync)
                return _activeSession;
        }
    }

    public TelemetrySessionRun? ActiveRun
    {
        get
        {
            lock (_sync)
                return _activeRun;
        }
    }

    public bool HasActiveSession => ActiveSession is { IsEnded: false };
    public bool HasActiveRun => ActiveRun is not null;

    public event Action? SessionsChanged;
    public event Action<TelemetrySession?>? ActiveSessionChanged;

    public TelemetrySessionManager()
    {
        _sessionsRoot = Path.Combine(FileSystem.AppDataDirectory, "Sessions");
        _indexPath = Path.Combine(_sessionsRoot, "sessions_index.json");
        Directory.CreateDirectory(_sessionsRoot);

        LoadIndex();
    }

    public static string CreateDefaultSessionName() => DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

    public TelemetrySession StartNewSession(string? requestedName = null, bool isTest = false, string? driverName = null)
    {
        var name = string.IsNullOrWhiteSpace(requestedName)
            ? CreateDefaultSessionName()
            : requestedName.Trim();

        var safeName = MakeSafeFileName(name);
        var directory = Path.Combine(_sessionsRoot, $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}");
        Directory.CreateDirectory(directory);

        var session = new TelemetrySession
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            IsTest = isTest,
            StartedAtLocal = DateTime.Now,
            DirectoryPath = directory,
            DriverName = driverName
        };

        lock (_sync)
        {
            _activeSession = session;
            _activeRun = null;
            Sessions.Insert(0, session);
        }

        SaveIndex();
        SessionsChanged?.Invoke();
        ActiveSessionChanged?.Invoke(session);
        return session;
    }

    public TelemetrySessionRun StartRun(string source, bool markSessionAsTest = false)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Źródło połączenia nie może być puste.", nameof(source));

        TelemetrySession session;
        TelemetrySessionRun run;

        lock (_sync)
        {
            session = _activeSession ?? throw new InvalidOperationException("Najpierw rozpocznij sesję.");

            if (session.IsEnded)
                throw new InvalidOperationException("Ta sesja jest już zakończona. Rozpocznij nową sesję albo wybierz kontynuację przed połączeniem.");

            if (_activeRun is not null)
                throw new InvalidOperationException("Połączenie w tej sesji już trwa.");

            if (markSessionAsTest)
                session.IsTest = true;

            Directory.CreateDirectory(session.DirectoryPath);

            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{MakeSafeFileName(source)}.rfdlog";
            run = new TelemetrySessionRun
            {
                Id = Guid.NewGuid().ToString("N"),
                Source = source,
                StartedAtLocal = DateTime.Now,
                RawLogPath = Path.Combine(session.DirectoryPath, fileName)
            };

            session.Runs.Add(run);
            _activeRun = run;
        }

        SaveIndex();
        SessionsChanged?.Invoke();
        ActiveSessionChanged?.Invoke(session);
        return run;
    }

    public void StopActiveRun()
    {
        TelemetrySession? session;

        lock (_sync)
        {
            if (_activeRun is null)
                return;

            _activeRun.StoppedAtLocal = DateTime.Now;
            _activeRun = null;
            session = _activeSession;
        }

        SaveIndex();
        SessionsChanged?.Invoke();
        ActiveSessionChanged?.Invoke(session);
    }

    public void EndActiveSession()
    {
        TelemetrySession? endedSession;

        lock (_sync)
        {
            if (_activeRun is not null)
                throw new InvalidOperationException("Najpierw zatrzymaj połączenie/symulator, dopiero potem zakończ sesję.");

            if (_activeSession is null)
                return;

            _activeSession.EndedAtLocal ??= DateTime.Now;
            endedSession = _activeSession;
            _activeSession = null;
        }

        SaveIndex();
        SessionsChanged?.Invoke();
        ActiveSessionChanged?.Invoke(null);
    }

    public void RefreshFromDisk()
    {
        LoadIndex();
        SessionsChanged?.Invoke();
        ActiveSessionChanged?.Invoke(ActiveSession);
    }

    public string GetExportDirectoryForActiveSession()
    {
        var session = ActiveSession;
        var baseDirectory = session?.DirectoryPath ?? Path.Combine(FileSystem.AppDataDirectory, "Exports");
        var directory = Path.Combine(baseDirectory, "Exports", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void LoadIndex()
    {
        lock (_sync)
        {
            Sessions.Clear();

            if (!File.Exists(_indexPath))
                return;

            try
            {
                var json = File.ReadAllText(_indexPath);
                var loaded = JsonSerializer.Deserialize<List<TelemetrySession>>(json, _jsonOptions) ?? new List<TelemetrySession>();

                foreach (var session in loaded.OrderByDescending(session => session.StartedAtLocal))
                    Sessions.Add(session);

                _activeSession = Sessions.FirstOrDefault(session => !session.IsEnded);
                _activeRun = null;
            }
            catch
            {
                _activeSession = null;
                _activeRun = null;
            }
        }
    }

    private void SaveIndex()
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_sessionsRoot);
            var json = JsonSerializer.Serialize(Sessions.ToList(), _jsonOptions);
            File.WriteAllText(_indexPath, json);
        }
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var safe = Regex.Replace(value, $"[{invalid}]", "_");
        safe = Regex.Replace(safe, "\\s+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? "session" : safe;
    }
}
