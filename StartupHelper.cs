using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Searchhy;

/// <summary>
/// Helper to configure Searchhy to launch automatically on Windows login in the background.
/// </summary>
public static class StartupHelper
{
    private const string AppName = "Searchhy";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Checks if Searchhy is registered to start on Windows boot.
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers Searchhy in the Windows CurrentUser Run key for auto-start.
    /// </summary>
    public static void EnableAutoStart()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.SetValue(AppName, $"\"{exePath}\"");
                Logger.LogInfo("Auto-start on Windows login enabled.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to enable auto-start", ex);
        }
    }

    /// <summary>
    /// Removes Searchhy from the Windows CurrentUser Run key.
    /// </summary>
    public static void DisableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(AppName, false);
            Logger.LogInfo("Auto-start on Windows login disabled.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to disable auto-start", ex);
        }
    }
}
