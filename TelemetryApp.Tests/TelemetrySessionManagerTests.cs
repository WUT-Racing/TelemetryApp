using System.Reflection;
using Moq;
using TelemetryApp.Backend.Sessions;
using Xunit;

namespace TelemetryApp.Backend.Tests;

[Collection("Sequential FileSystem")]
public class TelemetrySessionManagerTests : IDisposable
{
    private readonly string _testTempPath;

    public TelemetrySessionManagerTests()
    {
        _testTempPath = Path.Combine(Path.GetTempPath(), "TelemetrySessionTests_", Guid.NewGuid().ToString("N"));
        MockMauiFileSystem(_testTempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempPath))
        {
            try
            {
                Directory.Delete(_testTempPath, true);
            }
            catch { /* Ignoruj zablokowane pliki */ }
        }
    }

    private static void MockMauiFileSystem(string fakeAppDataPath)
    {
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem.Setup(f => f.AppDataDirectory).Returns(fakeAppDataPath);

        var fileSystemType = typeof(FileSystem);
        var setCurrentMethod = fileSystemType.GetMethod("SetCurrent", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        
        if (setCurrentMethod != null)
        {
            setCurrentMethod.Invoke(null, new object[] { mockFileSystem.Object });
        }
        else
        {
            var currentField = fileSystemType.GetField("currentImplementation", BindingFlags.Static | BindingFlags.NonPublic) 
                            ?? fileSystemType.GetField("s_current", BindingFlags.Static | BindingFlags.NonPublic);
            
            currentField?.SetValue(null, mockFileSystem.Object);
        }
    }

    [Fact]
    public void StartNewSession_CreatesSessionAndSetsAsActive()
    {
        var manager = new TelemetrySessionManager();
        var sessionName = "Test Session 1";

        var session = manager.StartNewSession(requestedName: sessionName, isTest: true, driverName: "Jan");

        Assert.NotNull(session);
        Assert.Equal(sessionName, session.Name);
        Assert.True(session.IsTest);
        Assert.Equal("Jan", session.DriverName);
        
        Assert.True(manager.HasActiveSession);
        Assert.Same(session, manager.ActiveSession);
        Assert.Single(manager.Sessions);
        
        Assert.True(Directory.Exists(session.DirectoryPath));
    }

    [Fact]
    public void StartRun_WhenNoActiveSession_ThrowsInvalidOperationException()
    {
        var manager = new TelemetrySessionManager(); // ActiveSession jest null

        var ex = Assert.Throws<InvalidOperationException>(() => manager.StartRun("COM1"));
        Assert.Contains("Najpierw rozpocznij sesję", ex.Message);
    }

    [Fact]
    public void StartRun_WithActiveSession_CreatesRunAndSetsAsActive()
    {
        var manager = new TelemetrySessionManager();
        var session = manager.StartNewSession("Run Test");

        var run = manager.StartRun("Symulator");

        Assert.NotNull(run);
        Assert.Equal("Symulator", run.Source);
        
        Assert.True(manager.HasActiveRun);
        Assert.Same(run, manager.ActiveRun);
        Assert.Single(session.Runs);
        Assert.Null(run.StoppedAtLocal);
    }

    [Fact]
    public void StartRun_WhenRunAlreadyActive_ThrowsInvalidOperationException()
    {
        var manager = new TelemetrySessionManager();
        manager.StartNewSession("Multiple Run Test");
        manager.StartRun("COM1"); // Pierwszy run aktywny

        var ex = Assert.Throws<InvalidOperationException>(() => manager.StartRun("COM2"));
        Assert.Contains("Połączenie w tej sesji już trwa", ex.Message);
    }

    [Fact]
    public void StopActiveRun_ClearsActiveRunAndSetsStopTime()
    {
        var manager = new TelemetrySessionManager();
        manager.StartNewSession("Stop Test");
        var run = manager.StartRun("COM1");

        manager.StopActiveRun();

        Assert.False(manager.HasActiveRun);
        Assert.Null(manager.ActiveRun);
        Assert.NotNull(run.StoppedAtLocal);
    }

    [Fact]
    public void EndActiveSession_WithActiveRun_ThrowsInvalidOperationException()
    {
        var manager = new TelemetrySessionManager();
        manager.StartNewSession("End Test");
        manager.StartRun("COM1"); // Run jest nadal aktywny

        var ex = Assert.Throws<InvalidOperationException>(() => manager.EndActiveSession());
        Assert.Contains("Najpierw zatrzymaj połączenie/symulator", ex.Message);
    }

    [Fact]
    public void EndActiveSession_WithoutActiveRun_EndsSessionSuccessfully()
    {
        var manager = new TelemetrySessionManager();
        var session = manager.StartNewSession("End Test");

        manager.EndActiveSession();

        Assert.False(manager.HasActiveSession);
        Assert.Null(manager.ActiveSession);
        Assert.True(session.IsEnded);
        Assert.NotNull(session.EndedAtLocal);
    }

    [Fact]
    public void StateIsPersisted_AndReloaded_BetweenManagerInstances()
    {
        var manager1 = new TelemetrySessionManager(); // Instancja 1 weryfikuje index_path
        manager1.StartNewSession("Persisted Session");
        
        // Zatrzymanie sesji aby zapisała się do JSON
        manager1.EndActiveSession();

        var manager2 = new TelemetrySessionManager(); // Instancja 2 wymusza ponowne odczytanie LoadIndex()

        Assert.Single(manager2.Sessions);
        var loadedSession = manager2.Sessions.First();
        Assert.Equal("Persisted Session", loadedSession.Name);
        Assert.True(loadedSession.IsEnded);
    }

    [Fact]
    public void GetExportDirectoryForActiveSession_CreatesAndReturnsValidPath()
    {
        var manager = new TelemetrySessionManager();
        manager.StartNewSession("Export Test");

        var exportDir = manager.GetExportDirectoryForActiveSession();

        Assert.NotNull(exportDir);
        Assert.True(Directory.Exists(exportDir));
        Assert.Contains("Exports", exportDir);
    }
}