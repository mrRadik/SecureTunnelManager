using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class JumpHostListItemViewModel : ObservableObject
{
    public JumpHostListItemViewModel(
        JumpHost jumpHost,
        int referenceCount,
        string endpointDisplay,
        string referenceCountDisplay)
    {
        JumpHost = jumpHost;
        ReferenceCount = referenceCount;
        EndpointDisplay = endpointDisplay;
        ReferenceCountDisplay = referenceCountDisplay;
    }

    public JumpHost JumpHost { get; }
    public int ReferenceCount { get; }
    public string EndpointDisplay { get; }
    public string ReferenceCountDisplay { get; }
    public string Name => JumpHost.Name;
}

public partial class JumpHostsViewModel : ObservableObject
{
    private readonly IJumpHostService _jumpHostService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;
    private readonly INotificationService _notifications;

    public JumpHostsViewModel(
        IJumpHostService jumpHostService,
        IDialogService dialogService,
        ILocalizationService localization,
        INotificationService notifications)
    {
        _jumpHostService = jumpHostService;
        _dialogService = dialogService;
        _localization = localization;
        _notifications = notifications;
        _localization.LanguageChanged += (_, _) => _ = LoadAsync();
    }

    public ObservableCollection<JumpHostListItemViewModel> Items { get; } = new();

    [ObservableProperty] private bool _isLoading;

    public bool HasItems => Items.Count > 0;
    public bool ShowEmptyState => !IsLoading && !HasItems;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        OnPropertyChanged(nameof(ShowEmptyState));

        try
        {
            var jumpHosts = await _jumpHostService.GetAllAsync().ConfigureAwait(true);
            var refCounts = await _jumpHostService.GetReferenceCountsAsync().ConfigureAwait(true);

            Items.Clear();
            foreach (var jumpHost in jumpHosts)
            {
                refCounts.TryGetValue(jumpHost.Id, out var refs);
                Items.Add(new JumpHostListItemViewModel(
                    jumpHost,
                    refs,
                    FormatEndpoint(jumpHost),
                    FormatReferenceCount(refs)));
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var created = await _dialogService.ShowJumpHostEditorAsync().ConfigureAwait(true);
        if (created is not null)
            await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EditAsync(JumpHostListItemViewModel? item)
    {
        if (item is null)
            return;

        var updated = await _dialogService.ShowJumpHostEditorAsync(item.JumpHost).ConfigureAwait(true);
        if (updated is not null)
            await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync(JumpHostListItemViewModel? item)
    {
        if (item is null)
            return;

        var message = item.ReferenceCount > 0
            ? _localization.Format("JumpHosts.DeleteConfirmInUse", item.Name, item.ReferenceCount)
            : _localization.Format("JumpHosts.DeleteConfirm", item.Name);

        if (!_dialogService.ShowConfirm(message, _localization.Get("JumpHosts.DeleteTitle"), destructiveConfirm: true))
            return;

        try
        {
            await _jumpHostService.DeleteAsync(item.JumpHost.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private string FormatReferenceCount(int count) =>
        count == 0
            ? _localization.Get("JumpHosts.NotUsed")
            : _localization.Format("JumpHosts.UsedByProfiles", count);

    private static string FormatEndpoint(JumpHost jumpHost)
    {
        var label = string.IsNullOrWhiteSpace(jumpHost.Username)
            ? jumpHost.Host
            : $"{jumpHost.Username}@{jumpHost.Host}";
        return jumpHost.Port == 22 ? label : $"{label}:{jumpHost.Port}";
    }
}
