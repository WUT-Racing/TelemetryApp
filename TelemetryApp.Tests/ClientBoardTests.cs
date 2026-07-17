using System.Reflection;
using Xunit;
using Moq;
using TelemetryApp.Backend.BoardConnection;
using TelemetryApp.Backend.Sessions;

namespace TelemetryApp.Backend.Tests
{
    public class ClientBoardTests : IDisposable
    {
        private readonly string _testTempPath;
        private const string FakeJsonDefinitions = @"{
            ""Frames"": [
                {
                    ""Id"": 1,
                    ""Name"": ""DummyFrame"",
                    ""Length"": 8,
                    ""Signals"": []
                }
            ]
        }";

        public ClientBoardTests()
        {
            _testTempPath = Path.Combine(Path.GetTempPath(), "TelemetryAppTests_", Guid.NewGuid().ToString("N"));
            
            MockMauiFileSystem(_testTempPath, FakeJsonDefinitions);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testTempPath))
            {
                try
                {
                    Directory.Delete(_testTempPath, true);
                }
                catch { /* Ignorowanie błędów blokowania plików przez asynchroniczne operacje I/O */ }
            }
        }

        private static void MockMauiFileSystem(string fakeAppDataPath, string fakeJsonContent)
        {
            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem.Setup(f => f.AppDataDirectory).Returns(fakeAppDataPath);
            mockFileSystem.Setup(f => f.OpenAppPackageFileAsync("sygnaly.json"))
                          .Returns(() => Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fakeJsonContent))));

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

        private (ClientBoard Board, TelemetrySessionManager Manager) CreateSutWithManager()
        {
            var sessionManager = new TelemetrySessionManager();
            var board = new ClientBoard(sessionManager);
            return (board, sessionManager);
        }

        private ClientBoard CreateSut()
        {
            return CreateSutWithManager().Board;
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenSessionManagerIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientBoard(null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ConnectAsync_ThrowsArgumentException_WhenPortNameIsNullOrWhiteSpace(string invalidPortName)
        {
            var board = CreateSut();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => board.ConnectAsync(invalidPortName));
            Assert.Contains("Nazwa portu nie może być pusta", ex.Message);
            Assert.False(board.IsConnected);
        }

        [Fact]
        public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
        {
            var board = CreateSut();

            var exception = await Record.ExceptionAsync(() => board.DisconnectAsync());

            Assert.Null(exception);
            Assert.False(board.IsConnected);
        }

        [Fact]
        public async Task DisposeAsync_CallsDisconnect_AndSilentlySucceeds()
        {
            var board = CreateSut();
            var eventRaised = false;
            
            board.OnStatusChanged += (msg) => eventRaised = true; 

            await board.DisposeAsync();

            Assert.False(board.IsConnected);
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task ConnectSimulatorAsync_WithActiveSession_ConnectsSuccessfullyAndRaisesEvent()
        {
            var (board, manager) = CreateSutWithManager();
            manager.StartNewSession("Sesja Testowa Symulatora");
            
            string? statusMessage = null;
            board.OnStatusChanged += (msg) => statusMessage = msg;

            await board.ConnectSimulatorAsync();

            Assert.True(board.IsConnected);
            Assert.NotNull(board.ActiveSession);
            Assert.True(manager.HasActiveRun);
            Assert.Contains("Uruchomiono symulator STM", statusMessage);

            await board.DisconnectAsync();
        }

        [Fact]
        public async Task ConnectSimulatorAsync_WithoutActiveSession_ThrowsInvalidOperationException()
        {
            var (board, manager) = CreateSutWithManager();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => board.ConnectSimulatorAsync());
            Assert.Contains("Najpierw rozpocznij sesję", ex.Message);
            Assert.False(board.IsConnected);
            Assert.False(manager.HasActiveRun);
        }

        [Fact]
        public async Task ConnectSimulatorAsync_WhenAlreadyConnected_ReturnsEarlyWithoutDuplicateRun()
        {
            var (board, manager) = CreateSutWithManager();
            manager.StartNewSession("Sesja Wielokrotnego Połączenia");
            
            await board.ConnectSimulatorAsync();
            var initialRunId = manager.ActiveRun?.Id;

            await board.ConnectSimulatorAsync();

            Assert.True(board.IsConnected);
            Assert.Equal(initialRunId, manager.ActiveRun?.Id);

            await board.DisconnectAsync();
        }

        [Fact]
        public async Task DisconnectAsync_WhenActive_ClearsStateAndStopsActiveRunInManager()
        {
            var (board, manager) = CreateSutWithManager();
            manager.StartNewSession("Sesja Rozłączenia");
            await board.ConnectSimulatorAsync();
            
            string? disconnectMessage = null;
            board.OnStatusChanged += (msg) => disconnectMessage = msg;

            await board.DisconnectAsync();

            Assert.False(board.IsConnected);
            Assert.False(manager.HasActiveRun);
            Assert.Contains("Rozłączono", disconnectMessage);
        }

        [Fact]
        public async Task ConnectAsync_WhenSerialPortFails_CleansUpActiveRunAndRethrowsException()
        {
            var (board, manager) = CreateSutWithManager();
            manager.StartNewSession("Sesja Awarii Portu");
            
            await Assert.ThrowsAnyAsync<Exception>(() => board.ConnectAsync("NIEISTNIEJACY_PORT_COM"));

            Assert.False(board.IsConnected);
            Assert.False(manager.HasActiveRun);
        }
    }
}