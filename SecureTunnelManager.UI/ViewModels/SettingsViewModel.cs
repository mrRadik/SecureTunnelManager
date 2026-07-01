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
    private readonly UpdatePromptService _updatePromptService;
    private bool _isLoading;

    public SettingsViewModel(
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        ILocalizationService localization,
        UpdatePromptService updatePromptService)
    {
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _localization = localization;
        _updatePromptService = updatePromptService;
    }

    [ObservableProperty]
    private bool _vaultAutoLockEnabled = true;

    [ObservableProperty]
    private int _vaultAutoLockMinutes = 15;

    [ObservableProperty]
    private int _reconnectIntervalSeconds = 15;

    [ObservableProperty]
    private int _circuitBreakerBreakSeconds = 90;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startAllTunnelsOnAppStart;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup = true;

    [ObservableProperty]
    private string _uiLanguage = "en";

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    [ObservableProperty]
    private bool _canCheckForUpdates;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [RelayCommand]
    public async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);
            VaultAutoLockEnabled = settings.VaultAutoLockEnabled;
            VaultAutoLockMinutes = settings.VaultAutoLockMinutes;
            ReconnectIntervalSeconds = settings.ReconnectIntervalSeconds;
            CircuitBreakerBreakSeconds = settings.CircuitBreakerBreakSeconds;
            StartAllTunnelsOnAppStart = settings.StartAllTunnelsOnAppStart;
            CloseToTray = settings.CloseToTray;
            CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
            StartWithWindows = _autoStartService.IsRegisteredWithWindows();
            UiLanguage = string.Equals(settings.UiLanguage, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            AppVersion = _updatePromptService.CurrentVersion;
            CanCheckForUpdates = _updatePromptService.CanCheckForUpdates;
            _localization.ApplyLanguage(UiLanguage);
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunUpdateCheck))]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        try
        {
            await _updatePromptService.CheckAndPromptAsync(silentWhenUpToDate: false).ConfigureAwait(true);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private bool CanRunUpdateCheck() => CanCheckForUpdates && !IsCheckingForUpdates;

    partial void OnCanCheckForUpdatesChanged(bool value) => CheckForUpdatesCommand.NotifyCanExecuteChanged();
    partial void OnIsCheckingForUpdatesChanged(bool value) => CheckForUpdatesCommand.NotifyCanExecuteChanged();

    partial void OnVaultAutoLockEnabledChanged(bool value) => _ = PersistAsync();
    partial void OnVaultAutoLockMinutesChanged(int value) => _ = PersistAsync();
    partial void OnReconnectIntervalSecondsChanged(int value) => _ = PersistAsync();
    partial void OnCircuitBreakerBreakSecondsChanged(int value) => _ = PersistAsync();
    partial void OnStartAllTunnelsOnAppStartChanged(bool value) => _ = PersistAsync();
    partial void OnCloseToTrayChanged(bool value) => _ = PersistAsync();
    partial void OnCheckForUpdatesOnStartupChanged(bool value) => _ = PersistAsync();

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
        settings.VaultAutoLockEnabled = VaultAutoLockEnabled;
        settings.VaultAutoLockMinutes = Math.Clamp(VaultAutoLockMinutes, 1, 1440);
        settings.ReconnectIntervalSeconds = Math.Clamp(ReconnectIntervalSeconds, 5, 300);
        settings.CircuitBreakerBreakSeconds = Math.Clamp(CircuitBreakerBreakSeconds, 30, 600);
        settings.StartAllTunnelsOnAppStart = StartAllTunnelsOnAppStart;
        settings.CloseToTray = CloseToTray;
        settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
        settings.UiLanguage = UiLanguage;
        await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(true);
    }
}
