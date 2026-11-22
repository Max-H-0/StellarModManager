using StellarModManager.Models;
using System;
using System.IO;
using System.Text.Json;

namespace StellarModManager.Services;

public static class SettingsService
{
    private static readonly string GameProfilePath = Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
    private static readonly string AppSettingsPath = Path.Combine(AppContext.BaseDirectory, "Data", "appsettings.json");


    public static void SaveGameProfile(GameProfile profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GameProfilePath)!);
        string json = JsonSerializer.Serialize(profile);
        File.WriteAllText(GameProfilePath, json);
    }

    public static GameProfile? LoadGameProfile()
    {
        if (!File.Exists(GameProfilePath))
            return null;

        string json = File.ReadAllText(GameProfilePath);
        return JsonSerializer.Deserialize<GameProfile>(json);
    }

    public static void SaveAppSettings(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppSettingsPath)!);
        string json = JsonSerializer.Serialize(settings);
        File.WriteAllText(AppSettingsPath, json);
    }

    public static AppSettings LoadAppSettings()
    {
        if (!File.Exists(AppSettingsPath))
            return new AppSettings();

        string json = File.ReadAllText(AppSettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}