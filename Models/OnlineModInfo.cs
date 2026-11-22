using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StellarModManager.Services;
using System;
using System.Collections.Generic; // Lists
using System.Collections.ObjectModel; // image collection
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StellarModManager.Models;

// Reuse for regular tags and ai tag
public record ModTag(string Text, bool IsAssisted = false, bool IsVibecoded = false, bool IsActive = false, bool IsInactive = false);

public partial class OnlineModInfo : ModInfo
{
    // I just put AI tags in here so they can't be used as regular tags
    private static readonly HashSet<string> ReservedTags = new(StringComparer.OrdinalIgnoreCase)  { "ai-free", "ai-assisted", "vibecoded", "active", "inactive" };

    public string RepoName { get; set; } = "";
    public string RepoOwner { get; set; } = "";

    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";

    [JsonIgnore] public List<ModTag> _Tags { get; private set; } = []; // TODO: please rename sensibly
    [JsonIgnore] public List<string> ImageUrls { get; set; } = [];
    [JsonIgnore] public string ThumbnailUrl { get; set; } = ""; // JsonIgnore is a "lazy fix hehe" :o
    [JsonIgnore] public bool GalleryLoaded { get; set; }
    [JsonIgnore] public ObservableCollection<Bitmap> GalleryImages { get; } = []; 
    [JsonIgnore] public ObservableCollection<ChangelogEntry> Changelog { get; } = [];
    [JsonIgnore] public Task? ChangelogTask { get; set; }

    [ObservableProperty] public partial Bitmap? ThumbnailImage { get; set; }
    [ObservableProperty] public partial double DownloadProgress { get; set; }

    [NotifyPropertyChangedFor(nameof(DownloadsText))]
    [ObservableProperty] public partial long Downloads { get; set; }

    [NotifyPropertyChangedFor(nameof(UpdatedText))]
    [ObservableProperty] public partial DateTime? LastUpdated { get; set; }

    [NotifyPropertyChangedFor(nameof(ShowInstalledState), nameof(ShowUpdateState), nameof(ShowNoState))]
    [ObservableProperty] public partial bool IsInstalling { get; set; }

    [NotifyPropertyChangedFor(nameof(ShowInstalledState), nameof(ShowUpdateState), nameof(ShowNoState))]
    [ObservableProperty] public partial bool IsInstalled { get; set; }

    [NotifyPropertyChangedFor(nameof(ShowInstalledState), nameof(ShowUpdateState))]
    [ObservableProperty] public partial bool HasUpdate { get; set; } // Set when the installed copy is older than this version

    [JsonIgnore] public string? RepoUrl => string.IsNullOrEmpty(RepoOwner) || string.IsNullOrEmpty(RepoName) ? null : $"https://github.com/{RepoOwner}/{RepoName}";
    [JsonIgnore] public string DownloadsText => string.Format(LocalizationService.Instance["DownloadsFormat"], Downloads);
    [JsonIgnore] public string UpdatedText => LastUpdated is DateTime updated ? Ago(updated) : "";
    // State column: exactly one of these (or IsInstalling) is shown
    [JsonIgnore] public bool ShowInstalledState => IsInstalled && !HasUpdate && !IsInstalling;
    [JsonIgnore] public bool ShowUpdateState => HasUpdate && !IsInstalling;
    [JsonIgnore] public bool ShowNoState => !IsInstalled && !IsInstalling;
    [JsonIgnore] public string InstallButtonText => IsInstalled ? LocalizationService.Instance["Reinstall"] : LocalizationService.Instance["Install"];

        
    public OnlineModInfo()
    {
        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(InstallButtonText));
            OnPropertyChanged(nameof(UpdatedText));
            OnPropertyChanged(nameof(DownloadsText));
            RebuildTags();
        };
    }


    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(InstallButtonText));
    }


    public void RebuildTags()
    {
        var list = new List<ModTag>();
        var loc = LocalizationService.Instance;

        // AI badge
        switch ((Ai ?? "").Trim().ToLowerInvariant())
        {
            case "ai-free": 
                list.Add(new ModTag(loc["AiFree"])); 
                break;

            case "ai-assisted": 
                list.Add(new ModTag(loc["AiAssisted"], IsAssisted: true)); 
                break;

            case "vibecoded": 
                list.Add(new ModTag(loc["AiVibecoded"], IsVibecoded: true)); 
                break;
        }

        switch ((Development ?? "").Trim().ToLowerInvariant())
        {
            case "active":
                list.Add(new ModTag(LocalizationService.Instance["DevelopmentActive"], IsActive: true));
                break;

            case "inactive":
                list.Add(new ModTag(LocalizationService.Instance["DevelopmentInactive"], IsInactive: true));
                break;
        }

        list.AddRange((Tags ?? [])
            .Select(tag => (tag ?? "").Trim())
            .Where(tag => tag.Length is > 0 and <= 24 && !ReservedTags.Contains(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(tag => new ModTag(tag)));

        _Tags = list;
        OnPropertyChanged(nameof(_Tags));
    }

    private static string Ago(DateTime updated)
    {
        var loc = LocalizationService.Instance;
        int days = (int)(DateTime.UtcNow - updated.ToUniversalTime()).TotalDays;

        return days switch
        {
            < 1 => loc["AgoToday"],
            1 => loc["AgoYesterday"],
            < 30 => string.Format(loc["AgoDays"], days),
            _ => updated.ToLocalTime().ToString("d", System.Globalization.CultureInfo.CurrentUICulture)
        };
    }
}