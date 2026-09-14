using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
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
        string referenceCountDisplay,
        string passwordExpiryDisplay)
    {
        JumpHost = jumpHost;
        ReferenceCount = referenceCount;
        EndpointDisplay = endpointDisplay;
        ReferenceCountDisplay = referenceCountDisplay;
        PasswordExpiryDisplay = passwordExpiryDisplay;
    }

    public JumpHost JumpHost { get; }
    public int ReferenceCount { get; }
    public string EndpointDisplay { get; }
    public string ReferenceCountDisplay { get; }
    public string PasswordExpiryDisplay { get; }
    public string Name => JumpHost.Name;
    public bool ShowPasswordExpiry => !string.IsNullOrEmpty(PasswordExpiryDisplay);
}

public partial class JumpHostsViewModel : ObservableObject
{
    private readonly IJumpHostService _jumpHostService;
    private readonly IJumpHostPasswordExpiryService _passwordExpiryService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;
    public JumpHostsViewModel(
        IJumpHostService jumpHostService,
        IJumpHostPasswordExpiryService passwordExpiryService,
        IDialogService dialogService,
        ILocalizationService localization)
    {
        _jumpHostService = jumpHostService;
        _passwordExpiryService = passwordExpiryService;
        _dialogService = dialogService;
        _localization = localization;
        _localization.LanguageChanged += (_, _) => _ = LoadAsync();
        _passwordExpiryService.PasswordExpiresAtUpdated += OnPasswordExpiresAtUpdated;
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
                    FormatReferenceCount(refs),
                    FormatPasswordExpiry(jumpHost)));
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public async Task EditByIdAsync(int jumpHostId)
    {
        var item = Items.FirstOrDefault(i => i.JumpHost.Id == jumpHostId);
        if (item is not null)
        {
            await EditAsync(item).ConfigureAwait(true);
            return;
        }

        var jumpHost = await _jumpHostService.GetByIdAsync(jumpHostId).ConfigureAwait(true);
        if (jumpHost is null)
            return;

        var updated = await _dialogService.ShowJumpHostEditorAsync(jumpHost).ConfigureAwait(true);
        if (updated is not null)
            await LoadAsync().ConfigureAwait(true);
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

    private void OnPasswordExpiresAtUpdated(object? sender, JumpHost jumpHost)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyPasswordExpiresAtUpdate(jumpHost);
            return;
        }

        dispatcher.InvokeAsync(() => ApplyPasswordExpiresAtUpdate(jumpHost), DispatcherPriority.Background);
    }

    private void ApplyPasswordExpiresAtUpdate(JumpHost jumpHost)
    {
        var item = Items.FirstOrDefault(i => i.JumpHost.Id == jumpHost.Id);
        if (item is null)
            return;

        var index = Items.IndexOf(item);
        if (index < 0)
            return;

        var refs = item.ReferenceCount;
        Items[index] = new JumpHostListItemViewModel(
            jumpHost,
            refs,
            FormatEndpoint(jumpHost),
            FormatReferenceCount(refs),
            FormatPasswordExpiry(jumpHost));
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

    private string FormatPasswordExpiry(JumpHost jumpHost)
    {
        if (!jumpHost.HasKnownPasswordExpiry)
            return string.Empty;

        var expires = jumpHost.PasswordExpiresAt!.Value;

        return _localization.Format("JumpHosts.PasswordExpires", FormatExpiryDate(expires));
    }

    private static string FormatExpiryDate(DateTime expires) =>
        expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
