using System.Collections.ObjectModel;
using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;

using SecureTunnelManager.Core.Services;

using SecureTunnelManager.UI.Services;



namespace SecureTunnelManager.UI.ViewModels;



public partial class MainViewModel : ObservableObject

{

    private readonly ITunnelProfileService _profileService;

    private readonly ITunnelManagerService _tunnelManager;

    private readonly IVaultService _vaultService;

    private readonly IDialogService _dialogService;

    private readonly ISettingsService _settingsService;

    private readonly ILocalizationService _localization;

    private readonly INotificationService _notificationService;

    private bool _tunnelAutoStartAttempted;

    private readonly HashSet<int> _startupQuietProfiles = new();

    private readonly Dictionary<int, TunnelStatus> _tunnelStatuses = new();

    public SettingsViewModel Settings { get; }

    public RdpViewModel Rdp { get; }

    public NotificationCenterViewModel Notifications { get; }



    public MainViewModel(

        ITunnelProfileService profileService,

        ITunnelManagerService tunnelManager,

        IVaultService vaultService,

        IDialogService dialogService,

        ISettingsService settingsService,

        ILocalizationService localization,

        INotificationService notificationService,

        NotificationCenterViewModel notifications,

        SettingsViewModel settings,

        RdpViewModel rdp)

    {

        _profileService = profileService;

        _tunnelManager = tunnelManager;

        _vaultService = vaultService;

        _dialogService = dialogService;

        _settingsService = settingsService;

        _localization = localization;

        _notificationService = notificationService;

        Notifications = notifications;

        Settings = settings;

        Rdp = rdp;



        _localization.LanguageChanged += (_, _) => RefreshLocalizedText();

        Rdp.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(RdpViewModel.ConnectedCount))
                RefreshStatusBar();
        };



        _tunnelManager.TunnelStateChanged += OnTunnelStateChanged;

        _vaultService.VaultLocked += OnVaultLocked;

        _vaultService.VaultUnlocked += (_, _) => RefreshVaultState();

        _vaultService.VaultReset += (_, _) => _ = LoadAsync();

        RefreshFilterSegments();

    }



    public ObservableCollection<TunnelRowViewModel> Tunnels { get; } = new();

    public ObservableCollection<TunnelRowViewModel> FilteredTunnels { get; } = new();



    [ObservableProperty]

    private NavigationSection _selectedSection = NavigationSection.Tunnels;



    [ObservableProperty]

    private TunnelRowViewModel? _selectedTunnel;



    [ObservableProperty]

    private bool _isVaultUnlocked;



    [ObservableProperty]

    private string _vaultStatusText = "Vault locked";



    [ObservableProperty]

    private bool _isBusy;



    [ObservableProperty]

    private string _searchText = string.Empty;



    [ObservableProperty]

    private TunnelListFilter _statusFilter = TunnelListFilter.All;



    public int TotalTunnelCount => Tunnels.Count;

    public int ConnectedCount => Tunnels.Count(t => t.Status == TunnelStatus.Connected);

    public int StoppedCount => Tunnels.Count(t => t.Status == TunnelStatus.Stopped);

    public int ReconnectingCount => Tunnels.Count(t => t.Status == TunnelStatus.Connecting);

    public int ErrorCount => Tunnels.Count(t => t.Status == TunnelStatus.Error);

    public string TunnelCountLabel => TotalTunnelCount == 1
        ? _localization.Get("Tunnels.TunnelCountOne")
        : _localization.Format("Tunnels.TunnelCountMany", TotalTunnelCount);

    public string ConnectedSummary => _localization.Get("Tunnels.Connected");

    public string StoppedSummary => _localization.Get("Tunnels.Stopped");

    public string ReconnectingSummary => _localization.Get("Tunnels.Reconnecting");

    public string ErrorSummary => _localization.Get("Tunnels.Error");

    public string TotalSummary => _localization.Get("Tunnels.Stats.Total");

    public ObservableCollection<FilterSegmentItem> TunnelFilterSegments { get; private set; } = new();

    public int ActiveConnectionsCount => ConnectedCount + Rdp.ConnectedCount;

    public string ActiveConnectionsText =>
        _localization.Format("StatusBar.ActiveConnections", ActiveConnectionsCount);

    public string VersionText =>
        AppVersion.ToLabel(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0));

    public string VaultActionText =>
        _localization.Get(IsVaultUnlocked ? "Tunnels.LockVault" : "Tunnels.UnlockVault");



    public bool HasTunnels => Tunnels.Count > 0;

    public bool HasFilteredTunnels => FilteredTunnels.Count > 0;

    public bool ShowEmptyState => !IsBusy && Tunnels.Count == 0;

    public bool ShowNoResults => !IsBusy && Tunnels.Count > 0 && FilteredTunnels.Count == 0;



    [RelayCommand]

    public async Task LoadAsync()

    {

        IsBusy = true;

        try

        {

            await Settings.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);

            RefreshVaultState();

            await Rdp.LoadAsync().ConfigureAwait(true);

            var profiles = await _profileService.GetAllAsync().ConfigureAwait(true);

            Tunnels.Clear();



            foreach (var profile in profiles)

            {

                var runtime = _tunnelManager.GetRuntimeState(profile.Id);

                Tunnels.Add(TunnelRowViewModel.FromProfile(profile, runtime));

            }



            ApplyFilter();

            NotifyTunnelListChanged();

            SyncTunnelStatusBaselines();

            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);

            var willAutoStart = !_tunnelAutoStartAttempted

                && settings.StartAllTunnelsOnAppStart

                && _vaultService.IsUnlocked

                && Tunnels.Count > 0;

            if (willAutoStart)

                BeginStartupQuietPeriod();

            await TryStartAllTunnelsOnAppStartAsync().ConfigureAwait(true);

            if (willAutoStart)

                FinalizeStartupQuietPeriod();

        }

        finally

        {

            IsBusy = false;

        }

    }



    private async Task RefreshTunnelListAsync()

    {

        var profiles = await _profileService.GetAllAsync().ConfigureAwait(true);

        Tunnels.Clear();



        foreach (var profile in profiles)

        {

            var runtime = _tunnelManager.GetRuntimeState(profile.Id);

            Tunnels.Add(TunnelRowViewModel.FromProfile(profile, runtime));

        }



        ApplyFilter();

        NotifyTunnelListChanged();

    }



    [RelayCommand]

    private void SetStatusFilter(TunnelListFilter filter)

    {

        StatusFilter = filter;

        ApplyFilter();

    }



    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnStatusFilterChanged(TunnelListFilter value) => ApplyFilter();



    private void ApplyFilter()

    {

        FilteredTunnels.Clear();

        var query = SearchText.Trim();



        foreach (var tunnel in Tunnels)

        {

            if (!MatchesFilter(tunnel))

                continue;



            if (!string.IsNullOrEmpty(query))

            {

                var haystack = $"{tunnel.Name} {tunnel.LocalEndpoint} {tunnel.JumpHostDisplay} {tunnel.DestinationDisplay}";

                if (!haystack.Contains(query, StringComparison.OrdinalIgnoreCase))

                    continue;

            }



            FilteredTunnels.Add(tunnel);

        }



        OnPropertyChanged(nameof(HasFilteredTunnels));

        OnPropertyChanged(nameof(ShowNoResults));

    }



    private bool MatchesFilter(TunnelRowViewModel tunnel) => StatusFilter switch

    {

        TunnelListFilter.Connected => tunnel.Status == TunnelStatus.Connected,

        TunnelListFilter.Stopped => tunnel.Status is TunnelStatus.Stopped,

        TunnelListFilter.Reconnecting => tunnel.Status == TunnelStatus.Connecting,

        TunnelListFilter.Error => tunnel.Status == TunnelStatus.Error,

        _ => true

    };



    private void UpdateStatistics()

    {

        OnPropertyChanged(nameof(TotalTunnelCount));

        OnPropertyChanged(nameof(TunnelCountLabel));

        OnPropertyChanged(nameof(ConnectedCount));

        OnPropertyChanged(nameof(StoppedCount));

        OnPropertyChanged(nameof(ReconnectingCount));

        OnPropertyChanged(nameof(ErrorCount));

        RefreshStatusBar();

        RefreshLocalizedText();

    }



    private void RefreshStatusBar()
    {
        OnPropertyChanged(nameof(ActiveConnectionsCount));
        OnPropertyChanged(nameof(ActiveConnectionsText));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(VaultActionText));
    }



    private void RefreshLocalizedText()

    {

        OnPropertyChanged(nameof(TunnelCountLabel));

        OnPropertyChanged(nameof(ConnectedSummary));

        OnPropertyChanged(nameof(StoppedSummary));

        OnPropertyChanged(nameof(ReconnectingSummary));

        OnPropertyChanged(nameof(ErrorSummary));

        OnPropertyChanged(nameof(TotalSummary));

        RefreshFilterSegments();

        RefreshVaultState();

    }



    private void RefreshFilterSegments()
    {
        TunnelFilterSegments = new ObservableCollection<FilterSegmentItem>(
        [
            new FilterSegmentItem(_localization.Get("Tunnels.FilterAll"), TunnelListFilter.All),
            new FilterSegmentItem(_localization.Get("Tunnels.FilterConnected"), TunnelListFilter.Connected),
            new FilterSegmentItem(_localization.Get("Tunnels.FilterStopped"), TunnelListFilter.Stopped),
            new FilterSegmentItem(_localization.Get("Tunnels.FilterReconnecting"), TunnelListFilter.Reconnecting),
            new FilterSegmentItem(_localization.Get("Tunnels.FilterError"), TunnelListFilter.Error)
        ]);
        OnPropertyChanged(nameof(TunnelFilterSegments));
    }



    private async Task TryStartAllTunnelsOnAppStartAsync()

    {

        if (_tunnelAutoStartAttempted)

            return;



        var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);

        if (!settings.StartAllTunnelsOnAppStart)

        {

            _tunnelAutoStartAttempted = true;

            return;

        }



        if (!_vaultService.IsUnlocked || Tunnels.Count == 0)

            return;



        _tunnelAutoStartAttempted = true;

        await _tunnelManager.StartAllAsync().ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateOnRow))]

    private async Task StartTunnelAsync(TunnelRowViewModel? row)

    {

        if (row is null) return;

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;

        await _tunnelManager.StartTunnelAsync(row.ProfileId).ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateOnRow))]

    private async Task StopTunnelAsync(TunnelRowViewModel? row)

    {

        if (row is null) return;

        await _tunnelManager.StopTunnelAsync(row.ProfileId).ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateOnRow))]

    private async Task RestartTunnelAsync(TunnelRowViewModel? row)

    {

        if (row is null) return;

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;

        await _tunnelManager.RestartTunnelAsync(row.ProfileId).ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateOnRow))]

    private async Task EditTunnelAsync(TunnelRowViewModel? row)

    {

        if (row is null) return;

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;



        var profile = await _profileService.GetByIdAsync(row.ProfileId).ConfigureAwait(true);

        if (profile is null) return;



        if (await _dialogService.ShowTunnelEditorAsync(profile).ConfigureAwait(true))

            await RefreshTunnelListAsync().ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateOnRow))]

    private async Task DuplicateTunnelAsync(TunnelRowViewModel? row)

    {

        if (row is null) return;

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;



        var profile = await _profileService.GetByIdAsync(row.ProfileId).ConfigureAwait(true);

        if (profile is null) return;



        var all = await _profileService.GetAllAsync().ConfigureAwait(true);

        var clone = ResourceCloneHelper.CloneTunnel(profile);

        clone.Name = ResourceCloneHelper.GenerateCopyName(profile.Name, all.Select(p => p.Name));

        clone.LocalPort = ResourceCloneHelper.ResolveTunnelLocalPort(clone.LocalPort, clone.LocalBindAddress, all);



        try

        {

            var newId = await _profileService.CreateAsync(clone).ConfigureAwait(true);

            await RefreshTunnelListAsync().ConfigureAwait(true);



            _notificationService.Publish(new AppNotification

            {

                Severity = NotificationSeverity.Success,

                MessageKey = "Notification.TunnelDuplicated",

                MessageArgs = [clone.Name],

                ActionKind = NotificationActionKind.EditTunnel,

                ResourceId = newId,

                ActionLabelKey = "Notification.Edit"

            });

        }

        catch (Exception ex)

        {

            _notificationService.Publish(new AppNotification

            {

                Severity = NotificationSeverity.Error,

                MessageKey = "Notification.DuplicateFailed",

                MessageArgs = [ex.Message]

            });

        }

    }



    public async Task EditTunnelByIdAsync(int profileId)

    {

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;



        var profile = await _profileService.GetByIdAsync(profileId).ConfigureAwait(true);

        if (profile is null) return;



        if (await _dialogService.ShowTunnelEditorAsync(profile).ConfigureAwait(true))

            await RefreshTunnelListAsync().ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateOnRow))]

    private async Task DeleteTunnelAsync(TunnelRowViewModel? row)

    {

        if (row is null) return;

        if (!_dialogService.ShowConfirm(
                _localization.Format("Tunnels.DeleteConfirm", row.Name),
                _localization.Get("Tunnels.DeleteTitle"),
                destructiveConfirm: true))
            return;

        await _tunnelManager.StopTunnelAsync(row.ProfileId).ConfigureAwait(true);

        await _profileService.DeleteAsync(row.ProfileId).ConfigureAwait(true);

        await RefreshTunnelListAsync().ConfigureAwait(true);

    }



    [RelayCommand(CanExecute = nameof(CanOperateTunnel))]

    private async Task StartSelectedAsync()

    {

        if (SelectedTunnel is null) return;

        await StartTunnelCommand.ExecuteAsync(SelectedTunnel);

    }



    [RelayCommand(CanExecute = nameof(CanOperateTunnel))]

    private async Task StopSelectedAsync()

    {

        if (SelectedTunnel is null) return;

        await StopTunnelCommand.ExecuteAsync(SelectedTunnel);

    }



    [RelayCommand(CanExecute = nameof(CanOperateTunnel))]

    private async Task RestartSelectedAsync()

    {

        if (SelectedTunnel is null) return;

        await RestartTunnelCommand.ExecuteAsync(SelectedTunnel);

    }



    [RelayCommand(CanExecute = nameof(CanOperateTunnel))]

    private async Task EditSelectedAsync()

    {

        if (SelectedTunnel is null) return;

        await EditTunnelCommand.ExecuteAsync(SelectedTunnel);

    }



    [RelayCommand(CanExecute = nameof(CanOperateTunnel))]

    private async Task DeleteSelectedAsync()

    {

        if (SelectedTunnel is null) return;

        await DeleteTunnelCommand.ExecuteAsync(SelectedTunnel);

    }



    [RelayCommand]

    private async Task AddTunnelAsync()

    {

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;

        if (await _dialogService.ShowTunnelEditorAsync().ConfigureAwait(true))

            await RefreshTunnelListAsync().ConfigureAwait(true);

    }



    [RelayCommand]

    private async Task StartAllAsync()

    {

        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true)) return;

        await _tunnelManager.StartAllAsync().ConfigureAwait(true);

    }



    [RelayCommand]

    private async Task StopAllAsync() => await _tunnelManager.StopAllAsync().ConfigureAwait(true);



    partial void OnSelectedSectionChanged(NavigationSection value)

    {

        if (value == NavigationSection.Settings)

            _ = Settings.LoadCommand.ExecuteAsync(null);

        else if (value == NavigationSection.Rdp)

            _ = Rdp.LoadAsync();

    }



    partial void OnIsBusyChanged(bool value)

    {

        OnPropertyChanged(nameof(ShowEmptyState));

        OnPropertyChanged(nameof(ShowNoResults));

    }



    [RelayCommand]

    private void NavigateToTunnels() => SelectedSection = NavigationSection.Tunnels;



    [RelayCommand]

    private void Navigate(NavigationSection section) => SelectedSection = section;



    [RelayCommand]
    private void NavigateToSettings() => SelectedSection = NavigationSection.Settings;



    [RelayCommand]
    private void LockVault()
    {
        _vaultService.Lock(manual: true);
        RefreshVaultState();
    }



    public Task PromptUnlockVaultAsync() => UnlockVaultAsync();



    [RelayCommand]
    private async Task UnlockVaultAsync()

    {

        if (await _dialogService.ShowUnlockVaultAsync().ConfigureAwait(true))
        {
            RefreshVaultState();
            await LoadAsync().ConfigureAwait(true);
        }

    }



    [RelayCommand]
    private async Task VaultStatusClickAsync()
    {
        if (IsVaultUnlocked)
            LockVault();
        else
            await UnlockVaultAsync().ConfigureAwait(true);
    }



    [RelayCommand]
    private async Task VaultActionAsync()
    {
        if (IsVaultUnlocked)
            LockVault();
        else
            await UnlockVaultAsync().ConfigureAwait(true);
    }



    [RelayCommand]
    private async Task OpenShareWizardAsync()
    {
        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            return;

        var importResult = await _dialogService.ShowShareWizardAsync().ConfigureAwait(true);
        if (importResult is null || importResult.TotalImported == 0)
            return;

        await RefreshTunnelListAsync().ConfigureAwait(true);
        await Rdp.LoadAsync().ConfigureAwait(true);

        _notificationService.Publish(new AppNotification
        {
            Severity = NotificationSeverity.Success,
            MessageKey = "Notification.ShareImportSuccess",
            MessageArgs = [importResult.TunnelsImported, importResult.RdpImported]
        });
    }

    private bool CanOperateTunnel() => SelectedTunnel is not null;

    private bool CanOperateOnRow(TunnelRowViewModel? row) => row is not null;



    partial void OnSelectedTunnelChanged(TunnelRowViewModel? value)

    {

        StartSelectedCommand.NotifyCanExecuteChanged();

        StopSelectedCommand.NotifyCanExecuteChanged();

        RestartSelectedCommand.NotifyCanExecuteChanged();

        EditSelectedCommand.NotifyCanExecuteChanged();

        DeleteSelectedCommand.NotifyCanExecuteChanged();

    }



    private void OnVaultLocked(object? sender, VaultLockedEventArgs e)
    {
        RefreshVaultState();

        if (e.IsManual)
            return;

        _notificationService.Publish(new AppNotification
        {
            Severity = NotificationSeverity.Warning,
            MessageKey = "Notification.VaultAutoLocked",
            ActionKind = NotificationActionKind.UnlockVault,
            ActionLabelKey = "Notification.UnlockVault"
        });
    }



    private void OnTunnelStateChanged(object? sender, TunnelRuntimeState state)

    {

        System.Windows.Application.Current.Dispatcher.Invoke(() =>

        {

            var row = Tunnels.FirstOrDefault(t => t.ProfileId == state.ProfileId);

            if (row is null)

            {

                row = new TunnelRowViewModel { ProfileId = state.ProfileId };

                Tunnels.Add(row);

            }



            row.UpdateFrom(state);

            ApplyFilter();

            NotifyTunnelListChanged();

            PublishTunnelStatusNotification(state);

        });

    }



    private void BeginStartupQuietPeriod()

    {

        _startupQuietProfiles.Clear();

        foreach (var row in Tunnels)

            _startupQuietProfiles.Add(row.ProfileId);

    }



    private void FinalizeStartupQuietPeriod()

    {

        SyncTunnelStatusBaselines();

        foreach (var row in Tunnels)

        {

            var runtime = _tunnelManager.GetRuntimeState(row.ProfileId);

            var status = runtime?.Status ?? TunnelStatus.Stopped;

            if (IsStableTunnelStatus(status))

                _startupQuietProfiles.Remove(row.ProfileId);

        }

    }



    private static bool IsStableTunnelStatus(TunnelStatus status)

        => status is TunnelStatus.Connected or TunnelStatus.Stopped or TunnelStatus.Error;



    private void SyncTunnelStatusBaselines()

    {

        foreach (var row in Tunnels)

        {

            var runtime = _tunnelManager.GetRuntimeState(row.ProfileId);

            _tunnelStatuses[row.ProfileId] = runtime?.Status ?? TunnelStatus.Stopped;

        }

    }



    private void PublishTunnelStatusNotification(TunnelRuntimeState state)

    {

        if (_startupQuietProfiles.Contains(state.ProfileId))

        {

            _tunnelStatuses[state.ProfileId] = state.Status;

            if (IsStableTunnelStatus(state.Status))

                _startupQuietProfiles.Remove(state.ProfileId);

            return;

        }



        if (!_tunnelStatuses.TryGetValue(state.ProfileId, out var previous))

        {

            _tunnelStatuses[state.ProfileId] = state.Status;

            return;

        }



        if (previous == state.Status)

            return;



        _tunnelStatuses[state.ProfileId] = state.Status;



        switch (state.Status)

        {

            case TunnelStatus.Connected:

                _notificationService.Publish(new AppNotification

                {

                    Severity = NotificationSeverity.Success,

                    MessageKey = previous == TunnelStatus.Error

                        ? "Notification.TunnelReconnected"

                        : "Notification.TunnelConnected",

                    MessageArgs = [state.Name]

                });

                break;



            case TunnelStatus.Error:

                _notificationService.Publish(new AppNotification

                {

                    Severity = NotificationSeverity.Error,

                    MessageKey = "Notification.TunnelError",

                    MessageArgs = [state.Name, state.ErrorMessage ?? string.Empty],

                    ActionKind = NotificationActionKind.EditTunnel,

                    ResourceId = state.ProfileId,

                    ActionLabelKey = "Notification.Edit"

                });

                break;



            case TunnelStatus.Stopped

                when previous is TunnelStatus.Connected or TunnelStatus.Connecting or TunnelStatus.Error:

                _notificationService.Publish(new AppNotification

                {

                    Severity = NotificationSeverity.Info,

                    MessageKey = "Notification.TunnelStopped",

                    MessageArgs = [state.Name]

                });

                break;

        }

    }



    private void NotifyTunnelListChanged()

    {

        UpdateStatistics();

        OnPropertyChanged(nameof(HasTunnels));

        OnPropertyChanged(nameof(ShowEmptyState));

        OnPropertyChanged(nameof(ShowNoResults));

    }



    private void RefreshVaultState()

    {

        IsVaultUnlocked = _vaultService.IsUnlocked;

        VaultStatusText = _localization.Get(IsVaultUnlocked ? "Tunnels.VaultUnlocked" : "Tunnels.VaultLocked");

        RefreshStatusBar();

    }



    private async Task<bool> EnsureVaultUnlockedAsync()

    {

        if (_vaultService.IsUnlocked)

        {

            _vaultService.NotifyActivity();

            return true;

        }



        if (await _vaultService.TryUnlockFromCacheAsync().ConfigureAwait(true))

            return true;



        return await _dialogService.ShowUnlockVaultAsync().ConfigureAwait(true);

    }

}

