using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StellarModManager.Services;

namespace StellarModManager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _checkProcessTimer = new() { Interval = TimeSpan.FromSeconds(10) }; // not sure what the ideal value here would be


    public MainWindowViewModel()
    {
        LoadAvailableThemes();
        LoadAvailableLanguages();

        var appSettings = SettingsService.LoadAppSettings();
        SelectedTheme = appSettings.Theme;
        SelectedLanguage = appSettings.Language;
        ConfirmBeforeRemove = appSettings.ConfirmBeforeRemove;
        AutoDeployDependencies = appSettings.AutoDeployDependencies;
        AutoCheckForModUpdates = appSettings.AutoCheckForModUpdates;
        AutoCheckForAppUpdates = appSettings.AutoCheckForAppUpdates;
        SortIndex = appSettings.ModSort;
        IsCompactView = appSettings.CompactModList;

        ThemeService.ApplyTheme(ThemeService.LoadTheme(SelectedTheme));

        if (!LocalizationService.Instance.TrySetLanguage(SelectedLanguage, out string usedLanguage))
        {
            SelectedLanguage = appSettings.Language = usedLanguage;
            SettingsService.SaveAppSettings(appSettings);
        }

        _statusTimer.Tick += (_, _) => DismissStatus();

        // Rebuild the dependency tooltips when the language changes
        LocalizationService.Instance.LanguageChanged += (_, _) => RefreshDependencyStatuses();

        _updateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _updateCheckTimer.Tick += async (_, _) => await RefreshMods();

        var profile = SettingsService.LoadGameProfile();

        if (profile != null)
        {
            GamePath = profile.GamePath;

            if (MelonLoaderDetector.CheckInstallation(profile.GamePath) is MelonLoaderStatus.Installed)
            {
                IsMelonLoaderValid = true;
                SetupRequired = false;

                OpenGameProfiles(profile.GamePath, profile.GamePath);
                _ = RefreshMods();
            }
            else
            {
                MelonLoaderStatusText = "Previously saved game path is no longer valid";
            }
        }

        ApplyAutoCheckTimerState();

        if (appSettings.AutoCheckForAppUpdates)
        {
            _ = CheckForAppUpdates();
        }


        _checkProcessTimer.Start();
        _checkProcessTimer.Tick += (_, _) => SearchGameProcessIfNull();
        SearchGameProcessIfNull();

    }


    [ObservableProperty] public partial string GamePath { get; set; } = "No game selected";
    [ObservableProperty] public partial string MelonLoaderStatusText { get; set; } = "";
    [ObservableProperty] public partial string? StatusMessage { get; set; } // Result of the last mod operation, shown in an info bar
    [ObservableProperty] public partial bool IsStatusError { get; set; }


    [RelayCommand]
    private void DismissStatus()
    {
        _statusTimer.Stop();
        StatusMessage = null;
    }


    // Successes fade out on their own; errors stay until dismissed
    private void ReportSuccess(string key, params object[] args) => ShowStatus(LocalizationService.Instance.Format(key, args), isError: false);
    private void ReportError(string key, params object[] args) => ShowStatus(LocalizationService.Instance.Format(key, args), isError: true);

    private void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;

        _statusTimer.Stop();
        if (!isError)
        {
            _statusTimer.Start();
        }
    }
}
