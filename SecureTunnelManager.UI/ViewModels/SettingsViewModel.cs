using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public enum NavigationSection
{
    Tunnels,
    Rdp,
    Share,
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

public enum RdpListFilter
{
    All,
    Connected,
    Disconnected,
    Error
}

public enum RdpViewMode
{
    Grid,
    List
}

public sealed class FilterSegmentItem
{
    public FilterSegmentItem(string label, object value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public object Value { get; }
}

/// <summary>
/// Group filter entry for tunnels/RDP toolbars. <see cref="IsAll"/> matches every group;
/// otherwise <see cref="GroupKey"/> is normalized (<c>""</c> = ungrouped).
/// </summary>
public sealed class ConnectionGroupFilterItem
{
    private ConnectionGroupFilterItem(string? groupKey, string label, bool isAll)
    {
        GroupKey = groupKey;
        Label = label;
        IsAll = isAll;
    }

    public static ConnectionGroupFilterItem CreateAll(string label) => new(null, label, isAll: true);

    public static ConnectionGroupFilterItem Create(string groupKey, string label) =>
        new(groupKey, label, isAll: false);

    public string? GroupKey { get; }
    public string Label { get; }
    public bool IsAll { get; }

    public override string ToString() => Label;
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themeService;
    private readonly UpdatePromptService _updatePromptService;
    private readonly IVaultService _vaultService;
    private readonly NotificationCenterViewModel _notifications;
    private bool _isLoading;

    public SettingsViewModel(
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        ILocalizationService localization,
        IThemeService themeService,
        UpdatePromptService updatePromptService,
        IVaultService vaultService,
        NotificationCenterViewModel notifications)
    {
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _localization = localization;
        _themeService = themeService;
        _updatePromptService = updatePromptService;
        _vaultService = vaultService;
        _notifications = notifications;
    }

    [ObservableProperty]
    private bool _vaultAutoLockEnabled = true;

    [ObservableProperty]
    private int _vaultAutoLockMinutes = 15;

    [ObservableProperty]
    private bool _rememberVaultOnThisDevice;

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
    private bool _showNotificationPopups = true;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup = true;

    [ObservableProperty]
    private string _uiLanguage = "en";

    [ObservableProperty]
    private string _uiTheme = AppThemeModes.Dark;

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
            RememberVaultOnThisDevice = settings.RememberVaultOnThisDevice;
            ReconnectIntervalSeconds = settings.ReconnectIntervalSeconds;
            CircuitBreakerBreakSeconds = settings.CircuitBreakerBreakSeconds;
            StartAllTunnelsOnAppStart = settings.StartAllTunnelsOnAppStart;
            CloseToTray = settings.CloseToTray;
            ShowNotificationPopups = settings.ShowNotificationPopups;
            CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
            StartWithWindows = _autoStartService.IsRegisteredWithWindows();
            UiLanguage = string.Equals(settings.UiLanguage, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            UiTheme = AppThemeModes.Normalize(settings.UiTheme);
            _themeService.ApplyTheme(UiTheme);
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
    partial void OnRememberVaultOnThisDeviceChanged(bool value)
    {
        if (_isLoading)
            return;

        _ = ApplyRememberSettingAsync();
    }
    partial void OnReconnectIntervalSecondsChanged(int value) => _ = PersistAsync();
    partial void OnCircuitBreakerBreakSecondsChanged(int value) => _ = PersistAsync();
    partial void OnStartAllTunnelsOnAppStartChanged(bool value) => _ = PersistAsync();
    partial void OnCloseToTrayChanged(bool value) => _ = PersistAsync();
    partial void OnShowNotificationPopupsChanged(bool value)
    {
        if (!value)
            _notifications.HideToast();

        _ = PersistAsync();
    }
    partial void OnCheckForUpdatesOnStartupChanged(bool value) => _ = PersistAsync();

    partial void OnUiLanguageChanged(string value)
    {
        if (_isLoading || string.IsNullOrWhiteSpace(value))
            return;

        _localization.ApplyLanguage(value);
        _ = PersistAsync();
    }

    partial void OnUiThemeChanged(string value)
    {
        if (_isLoading || string.IsNullOrWhiteSpace(value))
            return;

        var normalized = AppThemeModes.Normalize(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            UiTheme = normalized;
            return;
        }

        _themeService.ApplyTheme(normalized);
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
        settings.RememberVaultOnThisDevice = RememberVaultOnThisDevice;
        settings.ReconnectIntervalSeconds = Math.Clamp(ReconnectIntervalSeconds, 5, 300);
        settings.CircuitBreakerBreakSeconds = Math.Clamp(CircuitBreakerBreakSeconds, 30, 600);
        settings.StartAllTunnelsOnAppStart = StartAllTunnelsOnAppStart;
        settings.CloseToTray = CloseToTray;
        settings.ShowNotificationPopups = ShowNotificationPopups;
        settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
        settings.UiLanguage = UiLanguage;
        settings.UiTheme = AppThemeModes.Normalize(UiTheme);
        await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(true);
    }

    private async Task ApplyRememberSettingAsync()
    {
        if (_isLoading)
            return;

        if (RememberVaultOnThisDevice)
        {
            if (_vaultService.IsUnlocked)
                await _vaultService.ApplyRememberUnlockAsync(true).ConfigureAwait(true);
            else
                await PersistAsync().ConfigureAwait(true);

            return;
        }

        await _vaultService.ClearRememberUnlockAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ForgetSavedUnlockAsync()
    {
        RememberVaultOnThisDevice = false;
        await _vaultService.ClearRememberUnlockAsync().ConfigureAwait(true);
    }
}
