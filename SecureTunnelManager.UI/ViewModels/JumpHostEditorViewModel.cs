using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Core.Validation;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class JumpHostEditorViewModel : ObservableObject
{
    private readonly IJumpHostService _jumpHostService;
    private readonly ICredentialService _credentialService;
    private readonly IVaultService _vaultService;
    private readonly ILocalizationService _localization;

    public JumpHostEditorViewModel(
        IJumpHostService jumpHostService,
        ICredentialService credentialService,
        IVaultService vaultService,
        ILocalizationService localization)
    {
        _jumpHostService = jumpHostService;
        _credentialService = credentialService;
        _vaultService = vaultService;
        _localization = localization;
    }

    public JumpHost? SavedJumpHost { get; private set; }
    public int JumpHostId { get; private set; }
    public bool IsEditMode => JumpHostId > 0;
    public int? InitialCredentialId { get; private set; }
    public int? InitialKeyPassphraseCredentialId { get; private set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private AuthMethod _authMethod = AuthMethod.Password;
    [ObservableProperty] private string? _privateKeyPath;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _nameError = string.Empty;
    [ObservableProperty] private string _hostError = string.Empty;
    [ObservableProperty] private string _usernameError = string.Empty;
    [ObservableProperty] private string _credentialError = string.Empty;
    [ObservableProperty] private string _privateKeyError = string.Empty;

    public string Password { get; set; } = string.Empty;
    public string KeyPassphrase { get; set; } = string.Empty;

    public string WindowTitle => _localization.Get(IsEditMode
        ? "JumpHost.Editor.EditTitle"
        : "JumpHost.Editor.NewTitle");

    public void Initialize(JumpHost? existing)
    {
        JumpHostId = existing?.Id ?? 0;
        InitialCredentialId = existing?.CredentialId;
        InitialKeyPassphraseCredentialId = existing?.KeyPassphraseCredentialId;
        Name = existing?.Name ?? string.Empty;
        Host = existing?.Host ?? string.Empty;
        Port = existing?.Port ?? 22;
        Username = existing?.Username ?? string.Empty;
        AuthMethod = existing?.AuthMethod ?? AuthMethod.Password;
        PrivateKeyPath = existing?.PrivateKeyPath;
        Password = string.Empty;
        KeyPassphrase = string.Empty;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IsEditMode));
    }

    public bool IsPasswordAuth
    {
        get => AuthMethod == AuthMethod.Password;
        set { if (value) AuthMethod = AuthMethod.Password; }
    }

    public bool IsPrivateKeyAuth
    {
        get => AuthMethod == AuthMethod.PrivateKey;
        set
        {
            if (value)
                AuthMethod = AuthMethod.PrivateKey;
            else if (AuthMethod == AuthMethod.PrivateKey)
                AuthMethod = AuthMethod.Password;
        }
    }

    partial void OnAuthMethodChanged(AuthMethod value)
    {
        OnPropertyChanged(nameof(IsPasswordAuth));
        OnPropertyChanged(nameof(IsPrivateKeyAuth));
    }

    public bool DialogResult { get; private set; }
    public event EventHandler? RequestClose;

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        _vaultService.NotifyActivity();

        if (!Validate())
            return;

        try
        {
            var credentialName = BuildCredentialName(Name.Trim());
            int? credentialId = null;
            int? keyPassphraseCredentialId = null;

            if (AuthMethod == AuthMethod.Password)
            {
                credentialId = await UpsertPasswordCredentialAsync(
                    InitialCredentialId,
                    credentialName,
                    Username.Trim(),
                    Password).ConfigureAwait(true);
            }
            else
            {
                credentialId = null;
                keyPassphraseCredentialId = await UpsertOptionalSecretAsync(
                    InitialKeyPassphraseCredentialId,
                    $"{credentialName}/passphrase",
                    KeyPassphrase).ConfigureAwait(true);
            }

            var model = new JumpHost
            {
                Id = JumpHostId,
                Name = Name.Trim(),
                Host = Host.Trim(),
                Port = Port,
                Username = Username.Trim(),
                AuthMethod = AuthMethod,
                CredentialId = credentialId,
                PrivateKeyPath = PrivateKeyPath,
                KeyPassphraseCredentialId = keyPassphraseCredentialId
            };

            if (IsEditMode)
                await _jumpHostService.UpdateAsync(model).ConfigureAwait(true);
            else
                model.Id = await _jumpHostService.CreateAsync(model).ConfigureAwait(true);

            SavedJumpHost = model;
            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void BrowseKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = _localization.Get("Editor.PrivateKeyFilter")
        };

        if (dialog.ShowDialog() != true)
            return;

        PrivateKeyPath = dialog.FileName;
        PrivateKeyError = string.Empty;
    }

    private bool Validate()
    {
        var valid = true;
        NameError = string.Empty;
        HostError = string.Empty;
        UsernameError = string.Empty;
        CredentialError = string.Empty;
        PrivateKeyError = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = _localization.Get("Editor.Validation.NameRequired");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            HostError = _localization.Get("Editor.Validation.HostRequired");
            valid = false;
        }
        else if (!NetworkAddressValidator.IsValidHostOrIp(Host))
        {
            HostError = _localization.Get(NetworkAddressValidator.IsIpFormatAttempt(Host)
                ? "Editor.Validation.InvalidIpAddress"
                : "Editor.Validation.InvalidHost");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            UsernameError = _localization.Get("Editor.Validation.UsernameRequired");
            valid = false;
        }

        if (AuthMethod == AuthMethod.Password
            && string.IsNullOrEmpty(Password)
            && !(IsEditMode && InitialCredentialId.HasValue))
        {
            CredentialError = _localization.Get("Editor.Validation.PasswordRequired");
            valid = false;
        }

        if (AuthMethod == AuthMethod.PrivateKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            PrivateKeyError = _localization.Get("Editor.Validation.PrivateKeyRequired");
            valid = false;
        }

        return valid;
    }

    private static string BuildCredentialName(string jumpHostName) => $"jump-host/{jumpHostName}";

    private async Task<int> UpsertPasswordCredentialAsync(
        int? existingId,
        string credentialName,
        string username,
        string password)
    {
        var id = existingId
            ?? (await _credentialService.GetByNameAsync(credentialName).ConfigureAwait(true))?.Id;

        if (id.HasValue)
        {
            await _credentialService.UpdateAsync(
                id.Value,
                credentialName,
                username,
                string.IsNullOrEmpty(password) ? null : password).ConfigureAwait(true);
            return id.Value;
        }

        return await _credentialService.CreateAsync(credentialName, username, password).ConfigureAwait(true);
    }

    private async Task<int?> UpsertOptionalSecretAsync(int? existingId, string credentialName, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return existingId;

        var id = existingId
            ?? (await _credentialService.GetByNameAsync(credentialName).ConfigureAwait(true))?.Id;

        if (id.HasValue)
        {
            await _credentialService.UpdateAsync(id.Value, credentialName, "passphrase", secret).ConfigureAwait(true);
            return id;
        }

        return await _credentialService.CreateAsync(credentialName, "passphrase", secret).ConfigureAwait(true);
    }
}
