using System;
using System.IO;

namespace Searchhy;

/// <summary>
/// Thread-safe logger writing to %LOCALAPPDATA%\LensChord\log.txt
/// </summary>
public static class Logger
{
    private static readonly object LockObj = new();
    private static readonly string LogDirectory;
    private static readonly string LogFilePath;

    static Logger()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LogDirectory = Path.Combine(localAppData, "LensChord");
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            LogFilePath = Path.Combine(LogDirectory, "log.txt");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize logger directory: {ex.Message}");
            LogDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LogFilePath = Path.Combine(LogDirectory, "log.txt");
        }
    }

    public static void LogInfo(string message) => Log("INFO", message);
    public static void LogWarn(string message) => Log("WARN", message);
    public static void LogError(string message, Exception? ex = null)
    {
        var formatted = ex == null ? message : $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        Log("ERROR", formatted);
    }

    private static void Log(string level, string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{level}] {message}";

            lock (LockObj)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }

            System.Diagnostics.Debug.WriteLine(line);
        }
        catch
        {
            // Fail silently to avoid crashing hook or UI threads
        }
    }
}
