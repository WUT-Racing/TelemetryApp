using SQLite;
using System.Collections.Generic;

public class TestSession
{
    public TestSession() 
    {
        TelemetryPoints = new List<TelemetryPoint>();
    }
    
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; } 

    [Indexed]
    public string Name { get; set; } = string.Empty; 

    public DateTime StartTime { get; set; }
    
    public ICollection<TelemetryPoint> TelemetryPoints { get; set; } = new List<TelemetryPoint>(); 
}