using System;
using System.IO;
using System.Text.Json;

namespace Searchhy;

public class AppSettings
{
    public bool HasCompletedSetup { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool ShowTrayNotificationOnStart { get; set; } = true;
    public string PreferredSearchEngine { get; set; } = "Searchhy Visual AI";

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LensChord",
        "settings.json"
    );

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"Failed to load settings: {ex.Message}");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
            Logger.LogInfo("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save settings", ex);
        }
    }
}
