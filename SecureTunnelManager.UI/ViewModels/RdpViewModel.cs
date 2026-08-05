using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class RdpViewModel : ObservableObject
{
    private readonly IRdpTargetService _targetService;
    private readonly IRdpSessionService _sessionService;
    private readonly IVaultService _vaultService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;

    public RdpViewModel(
        IRdpTargetService targetService,
        IRdpSessionService sessionService,
        IVaultService vaultService,
        IDialogService dialogService,
        ILocalizationService localization)
    {
        _targetService = targetService;
        _sessionService = sessionService;
        _vaultService = vaultService;
        _dialogService = dialogService;
        _localization = localization;

        _sessionService.SessionStateChanged += OnSessionStateChanged;
        _vaultService.VaultLocked += (_, _) => RefreshVaultState();
        _vaultService.VaultUnlocked += (_, _) => RefreshVaultState();
        _vaultService.VaultReset += (_, _) => _ = LoadAsync();
        _localization.LanguageChanged += (_, _) => RefreshLocalizedText();

        RefreshVaultState();
    }

    public ObservableCollection<RdpTargetRowViewModel> Computers { get; } = new();
    public ObservableCollection<RdpTargetRowViewModel> FilteredComputers { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isVaultUnlocked;
    [ObservableProperty] private string _statusSummary = string.Empty;

    public bool ShowEmptyState => !IsBusy && Computers.Count == 0;
    public bool ShowNoResults => !IsBusy && Computers.Count > 0 && FilteredComputers.Count == 0;

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
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
            RefreshStatusSummary();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(ShowNoResults));
        }
    }

    private RdpTargetRowViewModel CreateRow(RdpTarget target) => new(_localization)
    {
        TargetId = target.Id,
        Name = target.Name,
        RdpHostDisplay = $"{target.RdpHost}:{target.RdpPort}"
    };

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredComputers.Clear();
        var query = SearchText?.Trim() ?? string.Empty;
        foreach (var computer in Computers)
        {
            if (string.IsNullOrEmpty(query)
                || computer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || computer.RdpHostDisplay.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredComputers.Add(computer);
            }
        }

        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoResults));
        RefreshStatusSummary();
    }

    private void RefreshVaultState()
    {
        IsVaultUnlocked = _vaultService.IsUnlocked;
    }

    private void RefreshLocalizedText()
    {
        foreach (var computer in Computers)
            computer.RefreshLocalized();
        RefreshStatusSummary();
    }

    private void RefreshStatusSummary()
    {
        var connected = Computers.Count(c => c.Status == RdpSessionStatus.Connected);
        StatusSummary = _localization.Format("Rdp.CountSummary", Computers.Count, connected);
    }

    private void OnSessionStateChanged(object? sender, RdpRuntimeState state)
    {
        void Apply()
        {
            var row = Computers.FirstOrDefault(c => c.TargetId == state.TargetId);
            if (row is null) return;
            row.ApplyRuntime(state);
            RefreshStatusSummary();
        }

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            Apply();
        else
            System.Windows.Application.Current?.Dispatcher.Invoke(Apply);
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
            _dialogService.ShowError(ex.Message);
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
            await LoadAsync().ConfigureAwait(true);
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

    private async Task<bool> EnsureVaultUnlockedAsync()
    {
        if (_vaultService.IsUnlocked)
            return true;

        return await _dialogService.ShowUnlockVaultAsync().ConfigureAwait(true);
    }
}
