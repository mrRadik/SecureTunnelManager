using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public enum NavigationSection
{
    Tunnels,
    Settings
}

public enum TunnelListFilter
{
    All,
    Connected,
    Stopped,
    Reconnecting,
    Error
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;
    private readonly ILocalizationService _localization;
    private bool _isLoading;

    public SettingsViewModel(
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        ILocalizationService localization)
    {
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _localization = localization;
    }

    [ObservableProperty]
    private int _vaultAutoLockMinutes = 15;

    [ObservableProperty]
    private bool _minimizeToTrayOnStart = true;

    [ObservableProperty]
    private bool _startMinimizedWithWindows;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startAllTunnelsOnAppStart;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private string _uiLanguage = "en";

    [RelayCommand]
    public async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);
            VaultAutoLockMinutes = settings.VaultAutoLockMinutes;
            MinimizeToTrayOnStart = settings.MinimizeToTrayOnStart;
            StartMinimizedWithWindows = settings.StartMinimizedWithWindows;
            StartAllTunnelsOnAppStart = settings.StartAllTunnelsOnAppStart;
            CloseToTray = settings.CloseToTray;
            StartWithWindows = _autoStartService.IsRegisteredWithWindows();
            UiLanguage = string.Equals(settings.UiLanguage, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            _localization.ApplyLanguage(UiLanguage);
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnVaultAutoLockMinutesChanged(int value) => _ = PersistAsync();
    partial void OnMinimizeToTrayOnStartChanged(bool value) => _ = PersistAsync();
    partial void OnStartMinimizedWithWindowsChanged(bool value) => _ = PersistAsync();
    partial void OnStartAllTunnelsOnAppStartChanged(bool value) => _ = PersistAsync();
    partial void OnCloseToTrayChanged(bool value) => _ = PersistAsync();

    partial void OnUiLanguageChanged(string value)
    {
        if (_isLoading || string.IsNullOrWhiteSpace(value))
            return;

        _localization.ApplyLanguage(value);
        _ = PersistAsync();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoading)
            return;

        if (value)
            _autoStartService.RegisterWithWindows();
        else
            _autoStartService.UnregisterFromWindows();

        _ = PersistAsync();
    }

    private async Task PersistAsync()
    {
        if (_isLoading)
            return;

        var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);
        settings.VaultAutoLockMinutes = VaultAutoLockMinutes;
        settings.MinimizeToTrayOnStart = MinimizeToTrayOnStart;
        settings.StartMinimizedWithWindows = StartMinimizedWithWindows;
        settings.StartAllTunnelsOnAppStart = StartAllTunnelsOnAppStart;
        settings.CloseToTray = CloseToTray;
        settings.UiLanguage = UiLanguage;
        await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(true);
    }
}
