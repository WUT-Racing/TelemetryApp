using SQLite;

public class SQLiteConnectionFactory
{

    public SQLiteConnection GetConnection(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path invalid");
        }
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var connection = new SQLiteConnection(databasePath);
        
        connection.CreateTable<TestSession>();
        connection.CreateTable<TelemetryPoint>();

        return connection;
    }
}