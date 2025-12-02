using System;

public class TelemetryPoint
{
    public int Id { get; set; }

    public int sessionId { get; set; }
    public TestSession session { get; set; }

    public DateTime Timestamp { get; set; }
}