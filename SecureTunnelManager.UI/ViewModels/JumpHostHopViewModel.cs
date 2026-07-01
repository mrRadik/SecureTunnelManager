using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Validation;

namespace SecureTunnelManager.UI.ViewModels;

public partial class JumpHostHopViewModel : ObservableObject
{
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private AuthMethod _authMethod = AuthMethod.Password;
    [ObservableProperty] private int? _credentialId;
    [ObservableProperty] private string? _privateKeyPath;
    [ObservableProperty] private int? _keyPassphraseCredentialId;

    [ObservableProperty] private string _hostError = string.Empty;
    [ObservableProperty] private string _usernameError = string.Empty;
    [ObservableProperty] private string _credentialError = string.Empty;
    [ObservableProperty] private string _privateKeyError = string.Empty;

    public string Password { get; set; } = string.Empty;
    public string KeyPassphrase { get; set; } = string.Empty;

    [ObservableProperty] private int _index;
    public string Title => $"Jump host {Index + 1}";

    [ObservableProperty] private bool _canRemove = true;

    partial void OnIndexChanged(int value) => OnPropertyChanged(nameof(Title));

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

    partial void OnHostChanged(string value)
    {
        HostError = string.Empty;
        FlowChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPortChanged(int value) => FlowChanged?.Invoke(this, EventArgs.Empty);

    partial void OnUsernameChanged(string value)
    {
        UsernameError = string.Empty;
        FlowChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnAuthMethodChanged(AuthMethod value)
    {
        OnPropertyChanged(nameof(IsPasswordAuth));
        OnPropertyChanged(nameof(IsPrivateKeyAuth));
        FlowChanged?.Invoke(this, EventArgs.Empty);
    }

    public string FlowDisplay => FormatEndpoint(Username, Host, Port, Title);

    public event EventHandler? FlowChanged;

    public static JumpHostHopViewModel FromModel(JumpHostHop hop, int index) => new()
    {
        Index = index,
        Host = hop.Host,
        Port = hop.Port,
        Username = hop.Username,
        AuthMethod = hop.AuthMethod,
        CredentialId = hop.CredentialId,
        PrivateKeyPath = hop.PrivateKeyPath,
        KeyPassphraseCredentialId = hop.KeyPassphraseCredentialId
    };

    public JumpHostHop ToModel() => new()
    {
        Host = Host.Trim(),
        Port = Port,
        Username = Username.Trim(),
        AuthMethod = AuthMethod,
        CredentialId = CredentialId,
        PrivateKeyPath = PrivateKeyPath,
        KeyPassphraseCredentialId = KeyPassphraseCredentialId
    };

    public bool Validate(bool isEditMode)
    {
        var valid = true;
        HostError = string.Empty;
        UsernameError = string.Empty;
        CredentialError = string.Empty;
        PrivateKeyError = string.Empty;

        if (string.IsNullOrWhiteSpace(Host))
        {
            HostError = "Host is required";
            valid = false;
        }
        else if (!NetworkAddressValidator.TryValidateHostOrIp(Host, out var hostError))
        {
            HostError = hostError;
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            UsernameError = "Username is required";
            valid = false;
        }

        if (AuthMethod == AuthMethod.Password && !CredentialId.HasValue && string.IsNullOrEmpty(Password))
        {
            CredentialError = "Password is required";
            valid = false;
        }

        if (AuthMethod == AuthMethod.PrivateKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            PrivateKeyError = "Private key file is required";
            valid = false;
        }

        return valid;
    }

    [RelayCommand]
    private void BrowseKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Private keys (*.pem;*.ppk;*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        PrivateKeyPath = dialog.FileName;
        PrivateKeyError = string.Empty;
    }

    private static string FormatEndpoint(string username, string host, int port, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(host))
            return placeholder;

        var label = string.IsNullOrWhiteSpace(username) ? host : $"{username}@{host}";
        return port == 22 ? label : $"{label}:{port}";
    }
}
