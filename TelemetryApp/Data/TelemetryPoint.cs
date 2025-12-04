using SQLite;

public class TelemetryPoint
{
    public TelemetryPoint() { }
    
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; } 
    
    public DateTime Timestamp { get; set; }
    
    [Indexed]
    public int SessionId { get; set; } 

    public double EngineTemperatureC { get; set; }
    public int RPM { get; set; }
}