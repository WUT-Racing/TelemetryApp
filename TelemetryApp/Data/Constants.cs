using System.IO;
using Microsoft.Maui.Storage;

public static class Constants
{
    public const string DatabaseFilename = "Telemetry.db";

    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
}
