using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class PasswordVaultViewModel : ObservableObject
{
    private readonly ICredentialService _credentialService;
    private readonly IVaultService _vaultService;
    private readonly IDialogService _dialogService;

    public PasswordVaultViewModel(
        ICredentialService credentialService,
        IVaultService vaultService,
        IDialogService dialogService)
    {
        _credentialService = credentialService;
        _vaultService = vaultService;
        _dialogService = dialogService;

        _vaultService.VaultLocked += (_, _) => RefreshState();
        _vaultService.VaultUnlocked += (_, _) => RefreshState();
        _vaultService.VaultReset += (_, _) => RefreshState();
    }

    public ObservableCollection<Credential> Credentials { get; } = new();

    [ObservableProperty]
    private bool _isVaultUnlocked;

    [ObservableProperty]
    private string _statusText = "Vault is locked";

    [RelayCommand]
    public async Task LoadAsync()
    {
        RefreshState();
        if (!IsVaultUnlocked)
        {
            Credentials.Clear();
            return;
        }

        var items = await _credentialService.GetAllAsync().ConfigureAwait(true);
        Credentials.Clear();
        foreach (var item in items.OrderBy(c => c.Name))
            Credentials.Add(item);
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (await _dialogService.ShowUnlockVaultAsync().ConfigureAwait(true))
            await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void LockVault()
    {
        _vaultService.Lock();
        RefreshState();
        Credentials.Clear();
    }

    private void RefreshState()
    {
        IsVaultUnlocked = _vaultService.IsUnlocked;
        StatusText = IsVaultUnlocked ? "Vault unlocked — stored credentials are available." : "Vault is locked. Unlock to view stored credentials.";
        OnPropertyChanged(nameof(IsVaultUnlocked));
    }
}
