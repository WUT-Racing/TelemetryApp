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

    public void SaveTelemetryPoint(TelemetryPoint point, string databasePath)
    {
        using (var connection = _connectionFactory.GetConnection(databasePath))
        {
            connection.Insert(point);
        }
    }

    public List<TelemetryPoint> GetSessionPoints(string databasePath)
    {
        using (var connection = _connectionFactory.GetConnection(databasePath))
        {
            return connection.Table<TelemetryPoint>().ToList();
        }
    }

    
}