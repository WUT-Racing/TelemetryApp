using System.Collections.Generic;
using System;

public class TestSession
{
    public int Id{get;set;}
    public required string Name{get;set;}
    public DateTime StartTime{get; set;}

    public ICollection<TelemetryPoint> TelemetryPoints{get;set;} = new List<TelemetryPoint>();
}