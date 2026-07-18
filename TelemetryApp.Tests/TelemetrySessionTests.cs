using TelemetryApp.Backend.Sessions;
using Xunit;

namespace TelemetryApp.Backend.Tests;

public class TelemetrySessionTests
{
    [Fact]
    public void IsEnded_ReturnsTrue_OnlyWhenEndedAtLocalHasValue()
    {
        var session = new TelemetrySession(); // EndedAtLocal = null

        Assert.False(session.IsEnded);

        session.EndedAtLocal = DateTime.Now;
        Assert.True(session.IsEnded);
    }

    [Fact]
    public void HasCompletedRun_ReturnsTrue_WhenAnyRunHasStoppedAtLocal()
    {
        var session = new TelemetrySession();
        
        var activeRun = new TelemetrySessionRun { StoppedAtLocal = null };
        var completedRun = new TelemetrySessionRun { StoppedAtLocal = DateTime.Now };

        Assert.False(session.HasCompletedRun);

        session.Runs.Add(activeRun);
        Assert.False(session.HasCompletedRun);

        session.Runs.Add(completedRun);
        Assert.True(session.HasCompletedRun);
    }

    [Fact]
    public void DisplayName_AppendsTestSuffix_WhenIsTestIsTrue()
    {
        var session = new TelemetrySession { Name = "Trening A", IsTest = false };

        Assert.Equal("Trening A", session.DisplayName);

        session.IsTest = true;
        Assert.Equal("Trening A (test)", session.DisplayName);
    }

    [Fact]
    public void StatusText_ReturnsCorrectStatusString()
    {
        var session = new TelemetrySession { EndedAtLocal = null };

        Assert.Equal("Aktywna", session.StatusText);

        session.EndedAtLocal = DateTime.Now;
        Assert.Equal("Zakończona", session.StatusText);
    }

    [Fact]
    public void SourceText_ReturnsDistinctSourcesOrPlaceholder()
    {
        var session = new TelemetrySession();

        Assert.Equal("Brak połączeń", session.SourceText);

        session.Runs.Add(new TelemetrySessionRun { Source = "COM3" });
        session.Runs.Add(new TelemetrySessionRun { Source = "com3" });
        session.Runs.Add(new TelemetrySessionRun { Source = "Symulator" });

        Assert.Equal("COM3, Symulator", session.SourceText);
    }
}