using SQLite;

public class SQLiteConnectionFactory
{
    private string _databasePath;

    public SQLiteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("databasePath cannot be null or empty", nameof(databasePath));

        _databasePath = databasePath;
        EnsureDirectoryAndTables();
    }

    private void EnsureDirectoryAndTables()
    {
        var dir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SQLiteConnection(_databasePath);
        conn.CreateTable<TelemetryPoint>();
    }

    public void ChangeDatabase(string newDatabasePath)
    {
        _databasePath = newDatabasePath;
        EnsureDirectoryAndTables();
    }
    public SQLiteConnection GetConnection()
    {
        var connection = new SQLiteConnection(_databasePath);
        connection.CreateTable<TelemetryPoint>();
        return connection;
    }
}