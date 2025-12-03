public class TelemetryPoint
{
    public int Id { get; set; } 
    
    public DateTime Timestamp { get; set; }
    
    public int SessionId { get; set; } 

    public required TestSession Session { get; set; } 
    
    public double EngineTemperatureC { get; set; }
    public int RPM { get; set; }
    public double VehicleSpeedKPH { get; set; }
}