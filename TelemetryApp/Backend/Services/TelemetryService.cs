using SQLite;
using System.Collections.Generic;
using System.Linq;


public class TelemetryService
{
    private readonly SQLiteConnectionFactory _connectionFactory;

    public TelemetryService(SQLiteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void SaveTelemetryPoint(TelemetryPoint point)
    {
        using (var connection = _connectionFactory.GetConnection())
        {
            connection.Insert(point);
        }
    }

    public void SaveTelemetryPointBatch(List<TelemetryPoint> telemetryPoints)
    {
        using (var connection = _connectionFactory.GetConnection())
        {
            connection.RunInTransaction(() => connection.InsertAll(telemetryPoints));
        }
    }

    public List<TelemetryPoint> GetSessionPoints()
    {
        using (var connection = _connectionFactory.GetConnection())
        {
            return connection.Table<TelemetryPoint>().ToList();
        }
    }

    public void ChangeDatabase(string newDatabasePath)
    {
        _connectionFactory.ChangeDatabase(newDatabasePath);
    }

    public string[] GetDataTypes()
    {
        //TODO
        return ["Prędkość", "Obroty", "Temperatura Oleju"];
    }
/*
    public async Task<List<SensorReading>> GetSensorReadingsForSessionAsync(int sessionId)
{
    // Tu beda pobrane sensory z bazy danych
    // Grupowanie po SensorId, sortowanie po Timestamp
    // Dla kazdej grupy utworzyc SensorReading z listami Timestamps i Values
    // Zwrocic liste SensorReading
}
*/
}