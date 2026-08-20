using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class RdpViewModel : ObservableObject
{
    private readonly IRdpTargetService _targetService;
    private readonly IRdpSessionService _sessionService;
    private readonly IVaultService _vaultService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localization;
    private readonly INotificationService _notificationService;
    private HashSet<string> _collapsedGroupKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, RdpSessionStatus> _sessionStatuses = new();

    public RdpViewModel(
        IRdpTargetService targetService,
        IRdpSessionService sessionService,
        IVaultService vaultService,
        IDialogService dialogService,
        ISettingsService settingsService,
        ILocalizationService localization,
        INotificationService notificationService)
    {
        _targetService = targetService;
        _sessionService = sessionService;
        _vaultService = vaultService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _localization = localization;
        _notificationService = notificationService;

        _sessionService.SessionStateChanged += OnSessionStateChanged;
        _vaultService.VaultLocked += (_, _) => RefreshVaultState();
        _vaultService.VaultUnlocked += (_, _) => RefreshVaultState();
        _vaultService.VaultReset += (_, _) => _ = LoadAsync();
        _localization.LanguageChanged += (_, _) => RefreshLocalizedText();

        RefreshVaultState();
        RefreshFilterSegments();
        RefreshViewModeSegments();
    }

    public ObservableCollection<RdpTargetRowViewModel> Computers { get; } = new();
    public ObservableCollection<RdpGroupRowViewModel> FilteredGroups { get; } = new();
    public ObservableCollection<FilterSegmentItem> RdpFilterSegments { get; private set; } = new();
    public ObservableCollection<FilterSegmentItem> ViewModeSegments { get; private set; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isVaultUnlocked;
    [ObservableProperty] private RdpListFilter _statusFilter = RdpListFilter.All;
    [ObservableProperty] private RdpViewMode _viewMode = RdpViewMode.Grid;

    public bool IsGridView => ViewMode == RdpViewMode.Grid;

    public int TotalCount => Computers.Count;
    public int ConnectedCount => Computers.Count(c => c.Status is RdpSessionStatus.Connected or RdpSessionStatus.Connecting);
    public int DisconnectedCount => Computers.Count(c => c.Status == RdpSessionStatus.Disconnected);
    public int ErrorCount => Computers.Count(c => c.Status == RdpSessionStatus.Error);

    public string TotalLabel => _localization.Get("Rdp.Stats.Total");
    public string ConnectedSummary => _localization.Get("Rdp.Stats.Connected");
    public string DisconnectedSummary => _localization.Get("Rdp.Stats.Disconnected");
    public string ErrorSummary => _localization.Get("Rdp.Stats.Error");

    public bool ShowEmptyState => !IsBusy && Computers.Count == 0;
    public bool ShowNoResults => !IsBusy && Computers.Count > 0 && FilteredComputerCount == 0;
    public bool ShowGrid => !IsBusy && !ShowEmptyState && !ShowNoResults;

    private int FilteredComputerCount => FilteredGroups.Sum(g => g.Computers.Count);

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);
            _collapsedGroupKeys = ParseCollapsedGroups(settings.RdpCollapsedGroupsJson);

            var targets = await _targetService.GetAllAsync().ConfigureAwait(true);
            Computers.Clear();

            foreach (var target in targets)
            {
                var row = CreateRow(target);
                var runtime = _sessionService.GetRuntimeState(target.Id);
                if (runtime is not null)
                    row.ApplyRuntime(runtime);
                Computers.Add(row);
            }

            ApplyFilter();
            RefreshStatistics();
        }
        finally
        {
            IsBusy = false;
            NotifyViewStateChanged();
        }
    }

    private RdpTargetRowViewModel CreateRow(RdpTarget target) => new(_localization)
    {
        TargetId = target.Id,
        Name = target.Name,
        Description = target.Description,
        GroupName = target.GroupName,
        IconKey = target.IconKey,
        RdpHostDisplay = $"{target.RdpHost}:{target.RdpPort}",
        RdpHost = target.RdpHost,
        RdpPort = target.RdpPort
    };

    partial void OnViewModeChanged(RdpViewMode value)
    {
        OnPropertyChanged(nameof(IsGridView));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnStatusFilterChanged(RdpListFilter value) => ApplyFilter();

    [RelayCommand]
    private void SetStatusFilter(RdpListFilter filter) => StatusFilter = filter;

    private void ApplyFilter()
    {
        DisposeGroupSubscriptions();
        FilteredGroups.Clear();

        var query = SearchText?.Trim() ?? string.Empty;
        var matching = Computers.Where(computer =>
        {
            if (!MatchesStatusFilter(computer))
                return false;

            return string.IsNullOrEmpty(query)
                || computer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || computer.RdpHostDisplay.Contains(query, StringComparison.OrdinalIgnoreCase)
                || computer.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(computer.GroupName)
                    && computer.GroupName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        var grouped = matching
            .GroupBy(c => RdpGroupKey.Normalize(c.GroupName))
            .OrderBy(g => RdpGroupKey.IsUngrouped(g.Key) ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var row = new RdpGroupRowViewModel(_localization)
            {
                GroupKey = group.Key,
                IsExpanded = !_collapsedGroupKeys.Contains(group.Key)
            };

            foreach (var computer in group.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                row.Computers.Add(computer);

            row.PropertyChanged += OnGroupPropertyChanged;
            row.RefreshHeader();
            FilteredGroups.Add(row);
        }

        NotifyViewStateChanged();
        RefreshStatistics();
    }

    private bool MatchesStatusFilter(RdpTargetRowViewModel computer) => StatusFilter switch
    {
        RdpListFilter.Connected => computer.Status is RdpSessionStatus.Connected or RdpSessionStatus.Connecting,
        RdpListFilter.Disconnected => computer.Status == RdpSessionStatus.Disconnected,
        RdpListFilter.Error => computer.Status == RdpSessionStatus.Error,
        _ => true
    };

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RdpGroupRowViewModel.IsExpanded) || sender is not RdpGroupRowViewModel group)
            return;

        if (group.IsExpanded)
            _collapsedGroupKeys.Remove(group.GroupKey);
        else
            _collapsedGroupKeys.Add(group.GroupKey);

        _ = PersistCollapsedGroupsAsync();
    }

    private void DisposeGroupSubscriptions()
    {
        foreach (var group in FilteredGroups)
            group.PropertyChanged -= OnGroupPropertyChanged;
    }

    private async Task PersistCollapsedGroupsAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);
            settings.RdpCollapsedGroupsJson = JsonSerializer.Serialize(_collapsedGroupKeys.OrderBy(k => k).ToList());
            await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(true);
        }
        catch
        {
            // Non-critical UI preference.
        }
    }

    private static HashSet<string> ParseCollapsedGroups(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void NotifyViewStateChanged()
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ShowGrid));
    }

    private void RefreshVaultState()
    {
        IsVaultUnlocked = _vaultService.IsUnlocked;
    }

    private void RefreshLocalizedText()
    {
        foreach (var computer in Computers)
            computer.RefreshLocalized();

        foreach (var group in FilteredGroups)
            group.RefreshHeader();

        RefreshFilterSegments();
        RefreshViewModeSegments();
        RefreshStatistics();
    }

    private void RefreshViewModeSegments()
    {
        ViewModeSegments = new ObservableCollection<FilterSegmentItem>(
        [
            new FilterSegmentItem(_localization.Get("Rdp.ViewMode.List"), RdpViewMode.List),
            new FilterSegmentItem(_localization.Get("Rdp.ViewMode.Grid"), RdpViewMode.Grid)
        ]);
        OnPropertyChanged(nameof(ViewModeSegments));
    }

    private void RefreshFilterSegments()
    {
        RdpFilterSegments = new ObservableCollection<FilterSegmentItem>(
        [
            new FilterSegmentItem(_localization.Get("Tunnels.FilterAll"), RdpListFilter.All),
            new FilterSegmentItem(_localization.Get("Rdp.Stats.Connected"), RdpListFilter.Connected),
            new FilterSegmentItem(_localization.Get("Rdp.Stats.Disconnected"), RdpListFilter.Disconnected),
            new FilterSegmentItem(_localization.Get("Tunnels.FilterError"), RdpListFilter.Error)
        ]);
        OnPropertyChanged(nameof(RdpFilterSegments));
    }

    private void RefreshStatistics()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ConnectedCount));
        OnPropertyChanged(nameof(DisconnectedCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(ConnectedSummary));
        OnPropertyChanged(nameof(DisconnectedSummary));
        OnPropertyChanged(nameof(ErrorSummary));
    }

    private void RefreshGroupHeadersForComputer(RdpTargetRowViewModel row)
    {
        var groupKey = RdpGroupKey.Normalize(row.GroupName);
        FilteredGroups.FirstOrDefault(g => g.GroupKey == groupKey)?.RefreshHeader();
    }

    private void OnSessionStateChanged(object? sender, RdpRuntimeState state)
    {
        void Apply()
        {
            var row = Computers.FirstOrDefault(c => c.TargetId == state.TargetId);
            if (row is null) return;
            row.ApplyRuntime(state);
            RefreshGroupHeadersForComputer(row);
            RefreshStatistics();
            PublishSessionStatusNotification(state);
        }

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            Apply();
        else
            System.Windows.Application.Current?.Dispatcher.Invoke(Apply);
    }

    private void PublishSessionStatusNotification(RdpRuntimeState state)
    {
        if (!_sessionStatuses.TryGetValue(state.TargetId, out var previous))
        {
            _sessionStatuses[state.TargetId] = state.Status;
            return;
        }

        if (previous == state.Status)
            return;

        _sessionStatuses[state.TargetId] = state.Status;

        switch (state.Status)
        {
            case RdpSessionStatus.Connected:
                _notificationService.Publish(new AppNotification
                {
                    Severity = NotificationSeverity.Success,
                    MessageKey = "Notification.RdpConnected",
                    MessageArgs = [state.Name]
                });
                break;

            case RdpSessionStatus.Error:
                _notificationService.Publish(new AppNotification
                {
                    Severity = NotificationSeverity.Error,
                    MessageKey = "Notification.RdpError",
                    MessageArgs = [state.Name, state.ErrorMessage ?? string.Empty],
                    ActionKind = NotificationActionKind.EditRdpTarget,
                    ResourceId = state.TargetId,
                    ActionLabelKey = "Notification.Edit"
                });
                break;

            case RdpSessionStatus.Disconnected
                when previous is RdpSessionStatus.Connected or RdpSessionStatus.Connecting or RdpSessionStatus.Error:
                _notificationService.Publish(new AppNotification
                {
                    Severity = NotificationSeverity.Info,
                    MessageKey = "Notification.RdpDisconnected",
                    MessageArgs = [state.Name]
                });
                break;
        }
    }

    [RelayCommand]
    private async Task AddComputerAsync()
    {
        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            return;

        if (await _dialogService.ShowRdpEditorAsync().ConfigureAwait(true))
            await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ConnectAsync(RdpTargetRowViewModel? row)
    {
        if (row is null) return;
        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            return;

        try
        {
            await _sessionService.ConnectAsync(row.TargetId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _notificationService.Publish(new AppNotification
            {
                Severity = NotificationSeverity.Error,
                MessageKey = "Notification.RdpConnectFailed",
                MessageArgs = [ex.Message]
            });
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync(RdpTargetRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            await _sessionService.DisconnectAsync(row.TargetId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task EditAsync(RdpTargetRowViewModel? row)
    {
        if (row is null) return;
        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            return;

        var target = await _targetService.GetByIdAsync(row.TargetId).ConfigureAwait(true);
        if (target is null) return;

        if (await _dialogService.ShowRdpEditorAsync(target).ConfigureAwait(true))
        {
            var updated = await _targetService.GetByIdAsync(row.TargetId).ConfigureAwait(true);
            if (updated is not null)
                _sessionService.SyncTargetMetadata(updated);

            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task DuplicateAsync(RdpTargetRowViewModel? row)
    {
        if (row is null) return;
        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            return;

        var source = await _targetService.GetByIdAsync(row.TargetId).ConfigureAwait(true);
        if (source is null) return;

        var all = await _targetService.GetAllAsync().ConfigureAwait(true);
        var clone = ResourceCloneHelper.CloneRdpTarget(source);
        clone.Name = ResourceCloneHelper.GenerateCopyName(source.Name, all.Select(t => t.Name));

        try
        {
            var newId = await _targetService.CreateAsync(clone).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);

            _notificationService.Publish(new AppNotification
            {
                Severity = NotificationSeverity.Success,
                MessageKey = "Notification.RdpDuplicated",
                MessageArgs = [clone.Name],
                ActionKind = NotificationActionKind.EditRdpTarget,
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

    public async Task EditByIdAsync(int targetId)
    {
        if (!await EnsureVaultUnlockedAsync().ConfigureAwait(true))
            return;

        var target = await _targetService.GetByIdAsync(targetId).ConfigureAwait(true);
        if (target is null) return;

        if (await _dialogService.ShowRdpEditorAsync(target).ConfigureAwait(true))
        {
            var updated = await _targetService.GetByIdAsync(targetId).ConfigureAwait(true);
            if (updated is not null)
                _sessionService.SyncTargetMetadata(updated);

            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(RdpTargetRowViewModel? row)
    {
        if (row is null) return;

        var confirm = _dialogService.ShowConfirm(
            _localization.Format("Rdp.DeleteConfirm", row.Name),
            _localization.Get("Rdp.DeleteTitle"));
        if (!confirm) return;

        try
        {
            await _sessionService.DisconnectAsync(row.TargetId).ConfigureAwait(true);
            await _targetService.DeleteAsync(row.TargetId).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MoveToGroupAsync(RdpTargetRowViewModel? row)
    {
        if (row is null) return;

        var existingGroups = await _targetService.GetGroupNamesAsync().ConfigureAwait(true);
        var selected = await _dialogService.PickRdpGroupAsync(
            _localization.Get("Rdp.Group.MoveTitle"),
            _localization.Format("Rdp.Group.MoveMessage", row.Name),
            existingGroups,
            row.GroupName).ConfigureAwait(true);

        if (selected is null)
            return;

        try
        {
            await _targetService.SetGroupNameAsync(row.TargetId, selected).ConfigureAwait(true);
            row.GroupName = RdpGroupKey.IsUngrouped(selected) ? null : selected;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RenameGroupAsync(RdpGroupRowViewModel? group)
    {
        if (group is null || RdpGroupKey.IsUngrouped(group.GroupKey))
            return;

        var existingGroups = await _targetService.GetGroupNamesAsync().ConfigureAwait(true);
        var selected = await _dialogService.PickRdpGroupAsync(
            _localization.Get("Rdp.Group.RenameTitle"),
            _localization.Format("Rdp.Group.RenameMessage", group.DisplayName),
            existingGroups,
            group.GroupKey,
            allowClear: false).ConfigureAwait(true);

        if (selected is null || string.Equals(selected, group.GroupKey, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _targetService.RenameGroupAsync(group.GroupKey, selected).ConfigureAwait(true);

            if (_collapsedGroupKeys.Remove(group.GroupKey))
            {
                if (!RdpGroupKey.IsUngrouped(selected))
                    _collapsedGroupKeys.Add(selected);
                await PersistCollapsedGroupsAsync().ConfigureAwait(true);
            }

            foreach (var computer in Computers.Where(c => RdpGroupKey.Normalize(c.GroupName) == group.GroupKey))
                computer.GroupName = RdpGroupKey.IsUngrouped(selected) ? null : selected;

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
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
