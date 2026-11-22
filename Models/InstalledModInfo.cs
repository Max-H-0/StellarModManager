using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace StellarModManager.Models;

public partial class InstalledModInfo : ModInfo
{
    [ObservableProperty] public partial bool IsRemoving { get; set; }
    [ObservableProperty] public partial bool IsUpdating { get; set; }
    [ObservableProperty] public partial double UpdateProgress { get; set; }
    [ObservableProperty] public partial string? LatestVersion { get; set; }
    [ObservableProperty] public partial string? UpdateNotes { get; set; }
    [ObservableProperty] public partial OnlineModInfo? Online { get; set; }

    [NotifyPropertyChangedFor(nameof(IsCurrent))]
    [ObservableProperty] public partial bool IsUpdateAvailable { get; set; }

    // Shown in the "!" tooltip, empty means no missing dependencies
    [NotifyPropertyChangedFor(nameof(HasDependencyWarning))]
    [ObservableProperty] public partial string DependencyWarning { get; set; } = "";
    
    [JsonIgnore] public string? ExternalPath { get; init; }
    [JsonIgnore] public bool IsExternal => ExternalPath != null;
    [JsonIgnore] public bool HasDependencyWarning => !string.IsNullOrEmpty(DependencyWarning);
    [JsonIgnore] public bool IsCurrent => !IsExternal && !IsUpdateAvailable;
}
