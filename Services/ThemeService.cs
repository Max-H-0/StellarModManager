using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using StellarModManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StellarModManager.Services;

public static class ThemeService
{
    private static readonly string ThemesFolder = Path.Combine(AppContext.BaseDirectory, "Data", "Themes");
    private static Dictionary<string, ThemeDefinition>? s_themeCache;


    public static void ApplyTheme(ThemeDefinition theme)
    {
        var resources = Application.Current!.Resources;

        Application.Current!.RequestedThemeVariant = theme.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;

        void SetBrush(string key, string hex)
        {
            var color = Color.Parse(hex);
            var resourceKey = $"Theme.{key}Brush";

            if (resources.TryGetValue(resourceKey, out var existing) && existing is SolidColorBrush brush)
            {
                brush.Color = color;
            }
            else
            {
                resources[resourceKey] = new SolidColorBrush(color);
            }
        }

        SetBrush("Background", theme.Background);
        SetBrush("Content", theme.Content);
        SetBrush("Panel", theme.Panel);
        SetBrush("Surface", theme.Surface);
        SetBrush("SurfaceHover", theme.SurfaceHover);
        SetBrush("Border", theme.Border);
        SetBrush("Accent", theme.Accent);
        SetBrush("AccentHover", theme.AccentHover);
        SetBrush("AccentPressed", theme.AccentPressed);
        SetBrush("AccentForeground", theme.AccentForeground);
        SetBrush("TextPrimary", theme.TextPrimary);
        SetBrush("TextSecondary", theme.TextSecondary);
        SetBrush("TextMuted", theme.TextMuted);
        SetBrush("Success", theme.Success);
        SetBrush("Danger", theme.Danger);
        SetBrush("DangerHover", theme.DangerHover);
        SetBrush("DangerPressed", theme.DangerPressed);

        // Set the icon color for SVGs to match the primary text color.
        resources["Theme.IconCss"] = $".icon-fill {{ fill: {theme.TextPrimary}; }} .icon-stroke {{ stroke: {theme.TextPrimary}; }}";

        // Fluent controls (checkboxes, focus rings, text selection) follow the theme accent
        var accent = Color.Parse(theme.Accent);
        foreach (var key in new[] { "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
                                    "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3" })
        {
            resources[key] = accent;
        }
    }

    public static string[] GetAvailableThemeNames() => LoadAllThemes().Keys.ToArray();

    public static ThemeDefinition LoadTheme(string themeName)
    {
        var themes = LoadAllThemes();
        return themes.TryGetValue(themeName, out var theme) ? theme : new ThemeDefinition();
    }


    private static Dictionary<string, ThemeDefinition> LoadAllThemes()
    {
        if (s_themeCache != null) return s_themeCache;
        s_themeCache = new(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(ThemesFolder)) return s_themeCache;

        foreach (var path in Directory.GetFiles(ThemesFolder, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(path);
                var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (theme != null && !string.IsNullOrWhiteSpace(theme.Name)) s_themeCache[theme.Name] = theme;
            }
            catch {} // Skip bad file 
        }

        return s_themeCache;
    }
}