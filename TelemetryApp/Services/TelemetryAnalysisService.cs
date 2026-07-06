using System;
using System.IO;
using System.Threading.Tasks;
using Python.Included;
using Python.Runtime;

namespace TelemetryApp.Services
{
    public class TelemetryAnalysisService
    {
        private bool _isPythonInitialized = false;

        public async Task InitializePythonAsync()
        {
            if (_isPythonInitialized) return;

            // W wersji 3.11.1 te metody po prostu działają
            await Installer.SetupPython();
            await Installer.TryInstallPip();

            await Installer.PipInstallModule("pandas");
            await Installer.PipInstallModule("numpy");
            await Installer.PipInstallModule("scipy");

            PythonEngine.Initialize();
            _isPythonInitialized = true;
        }

        public async Task<string> AnalyzeSessionAsync(string csvFilePath, string competitionType, string configJson)
        {
            if (!_isPythonInitialized)
            {
                await InitializePythonAsync();
            }

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pythonEnginePath = Path.Combine(appDirectory, "PythonEngine");
            string jsonResult = string.Empty;

            using (Py.GIL())
            {
                try
                {
                    dynamic sys = Py.Import("sys");
                    sys.path.append(pythonEnginePath);

                    dynamic coreApi = Py.Import("core_api");
                    
                    dynamic pyResult = coreApi.process_session(csvFilePath, competitionType, configJson);
                    
                    jsonResult = pyResult.ToString();
                }
                catch (PythonException ex)
                {
                    throw new Exception($"Błąd w module analitycznym (Python):\n{ex.Message}\n{ex.StackTrace}");
                }
            }

            return jsonResult;
        }
    }
}