using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class JumpHostHopViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public JumpHostHopViewModel(ILocalizationService localization)
    {
        _localization = localization;
    }

    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private AuthMethod _authMethod = AuthMethod.Password;
    [ObservableProperty] private JumpHost? _selectedSavedJumpHost;
    [ObservableProperty] private int? _selectedSavedJumpHostId;
    [ObservableProperty] private bool _isLibraryPasswordRevealed;
    [ObservableProperty] private string _libraryPassword = string.Empty;
    [ObservableProperty] private string _libraryError = string.Empty;

    [ObservableProperty] private int _index;
    public string Title => _localization.Format("Editor.JumpHostTitle", Index + 1);

    [ObservableProperty] private bool _canRemove = true;

    public bool HasLibraryPassword => !string.IsNullOrEmpty(LibraryPassword);
    public bool CanEditLibraryHost => SelectedSavedJumpHost is not null;
    public bool CanDeleteLibraryHost => CanEditLibraryHost;

    public string HostSummary => SelectedSavedJumpHost is null
        ? "—"
        : SelectedSavedJumpHost.Port == 22
            ? SelectedSavedJumpHost.Host
            : $"{SelectedSavedJumpHost.Host}:{SelectedSavedJumpHost.Port}";

    public string UserSummary => SelectedSavedJumpHost?.Username ?? "—";

    public string AuthSummary => SelectedSavedJumpHost?.AuthMethod == AuthMethod.PrivateKey
        ? _localization.Get("Editor.CertificateKeyFile")
        : _localization.Get("Editor.Password");

    public string LibraryPasswordDisplay => !HasLibraryPassword
        ? "—"
        : IsLibraryPasswordRevealed
            ? LibraryPassword
            : "••••••••";

    public string LibraryKeyFileSummary => string.IsNullOrWhiteSpace(SelectedSavedJumpHost?.PrivateKeyPath)
        ? "—"
        : SelectedSavedJumpHost!.PrivateKeyPath!;

    public bool ShowLibraryPasswordRow =>
        SelectedSavedJumpHost?.AuthMethod == AuthMethod.Password;

    public bool ShowLibraryKeyRow =>
        SelectedSavedJumpHost?.AuthMethod == AuthMethod.PrivateKey;

    public event EventHandler? FlowChanged;

    public int? PendingJumpHostEntityId { get; private set; }

    private Func<IEnumerable<JumpHost>>? _savedJumpHostsLookup;
    private Func<int, Task<string?>>? _credentialPasswordLoader;

    public Func<JumpHost, Task<JumpHost?>>? EditSavedJumpHostHandler { get; set; }
    public Func<JumpHostHopViewModel, Task>? DeleteSavedJumpHostHandler { get; set; }

    public void SetSavedJumpHostsLookup(Func<IEnumerable<JumpHost>> lookup) =>
        _savedJumpHostsLookup = lookup;

    public void SetCredentialPasswordLoader(Func<int, Task<string?>> loader) =>
        _credentialPasswordLoader = loader;

    partial void OnIndexChanged(int value) => OnPropertyChanged(nameof(Title));

    partial void OnSelectedSavedJumpHostChanged(JumpHost? value)
    {
        LibraryError = string.Empty;
        if (SelectedSavedJumpHostId != value?.Id)
            SelectedSavedJumpHostId = value?.Id;

        if (value is not null)
            ApplyFromJumpHost(value);

        NotifySummaryPropertiesChanged();
        OnPropertyChanged(nameof(CanEditLibraryHost));
        OnPropertyChanged(nameof(CanDeleteLibraryHost));
        _ = LoadLibraryPasswordAsync();
        FlowChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedSavedJumpHostIdChanged(int? value)
    {
        if (SelectedSavedJumpHost?.Id == value)
            return;

        if (value is null or <= 0)
        {
            if (SelectedSavedJumpHost is not null)
                SelectedSavedJumpHost = null;
            return;
        }

        var match = _savedJumpHostsLookup?.Invoke()?.FirstOrDefault(j => j.Id == value);
        if (match is not null)
            SelectedSavedJumpHost = match;
    }

    partial void OnIsLibraryPasswordRevealedChanged(bool value) =>
        OnPropertyChanged(nameof(LibraryPasswordDisplay));

    public string FlowDisplay
    {
        get
        {
            if (SelectedSavedJumpHost is not null)
                return FormatEndpoint(
                    SelectedSavedJumpHost.Username,
                    SelectedSavedJumpHost.Host,
                    SelectedSavedJumpHost.Port,
                    SelectedSavedJumpHost.Name);

            return FormatEndpoint(Username, Host, Port, Title);
        }
    }

    public static JumpHostHopViewModel FromModel(
        JumpHostHop hop,
        int index,
        ILocalizationService localization)
    {
        return new JumpHostHopViewModel(localization)
        {
            Index = index,
            Host = hop.Host,
            Port = hop.Port,
            Username = hop.Username,
            AuthMethod = hop.AuthMethod,
            PendingJumpHostEntityId = hop.JumpHostEntityId
        };
    }

    public void BindSavedJumpHost(JumpHost jumpHost)
    {
        PendingJumpHostEntityId = null;
        SelectedSavedJumpHost = jumpHost;
        SelectedSavedJumpHostId = jumpHost.Id;
    }

    public void TryBindPendingSavedJumpHost(IEnumerable<JumpHost> savedJumpHosts)
    {
        var id = PendingJumpHostEntityId ?? SelectedSavedJumpHostId;
        if (id is not int entityId || entityId <= 0)
            return;

        var match = savedJumpHosts.FirstOrDefault(j => j.Id == entityId);
        if (match is not null)
            BindSavedJumpHost(match);
    }

    public void RefreshSavedJumpHostFromList(IEnumerable<JumpHost> savedJumpHosts)
    {
        var id = SelectedSavedJumpHostId ?? PendingJumpHostEntityId;
        if (id is not int entityId || entityId <= 0)
            return;

        var match = savedJumpHosts.FirstOrDefault(j => j.Id == entityId);
        if (match is not null && !ReferenceEquals(SelectedSavedJumpHost, match))
            BindSavedJumpHost(match);
    }

    public void ClearLibrarySelection()
    {
        PendingJumpHostEntityId = null;
        SelectedSavedJumpHostId = null;
        SelectedSavedJumpHost = null;
        LibraryPassword = string.Empty;
        IsLibraryPasswordRevealed = false;
        NotifySummaryPropertiesChanged();
    }

    public void ApplyFromJumpHost(JumpHost jumpHost)
    {
        Host = jumpHost.Host;
        Port = jumpHost.Port;
        Username = jumpHost.Username;
        AuthMethod = jumpHost.AuthMethod;
        OnPropertyChanged(nameof(FlowDisplay));
    }

    [RelayCommand]
    private async Task EditSavedJumpHostAsync()
    {
        if (SelectedSavedJumpHost is null || EditSavedJumpHostHandler is null)
            return;

        var updated = await EditSavedJumpHostHandler(SelectedSavedJumpHost).ConfigureAwait(true);
        if (updated is not null)
            BindSavedJumpHost(updated);
    }

    [RelayCommand]
    private async Task DeleteSavedJumpHostAsync()
    {
        if (SelectedSavedJumpHost is null || DeleteSavedJumpHostHandler is null)
            return;

        await DeleteSavedJumpHostHandler(this).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleLibraryPasswordReveal() =>
        IsLibraryPasswordRevealed = !IsLibraryPasswordRevealed;

    public JumpHostHop ToModel()
    {
        if (SelectedSavedJumpHost is null)
            throw new InvalidOperationException("Jump host must be selected from the library.");

        return new JumpHostHop
        {
            JumpHostEntityId = SelectedSavedJumpHost.Id,
            Host = SelectedSavedJumpHost.Host,
            Port = SelectedSavedJumpHost.Port,
            Username = SelectedSavedJumpHost.Username,
            AuthMethod = SelectedSavedJumpHost.AuthMethod
        };
    }

    public bool Validate(bool isEditMode)
    {
        LibraryError = string.Empty;

        if (SelectedSavedJumpHost is null || SelectedSavedJumpHost.Id <= 0)
        {
            LibraryError = _localization.Get("Editor.Validation.JumpHostSelectRequired");
            return false;
        }

        return true;
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(FlowDisplay));
        OnPropertyChanged(nameof(AuthSummary));
        NotifySummaryPropertiesChanged();
    }

    private async Task LoadLibraryPasswordAsync()
    {
        LibraryPassword = string.Empty;
        IsLibraryPasswordRevealed = false;

        if (SelectedSavedJumpHost?.AuthMethod != AuthMethod.Password
            || SelectedSavedJumpHost.CredentialId is not int credentialId
            || _credentialPasswordLoader is null)
        {
            NotifySummaryPropertiesChanged();
            return;
        }

        LibraryPassword = await _credentialPasswordLoader(credentialId).ConfigureAwait(true) ?? string.Empty;
        NotifySummaryPropertiesChanged();
    }

    private void NotifySummaryPropertiesChanged()
    {
        OnPropertyChanged(nameof(HostSummary));
        OnPropertyChanged(nameof(UserSummary));
        OnPropertyChanged(nameof(AuthSummary));
        OnPropertyChanged(nameof(LibraryPasswordDisplay));
        OnPropertyChanged(nameof(LibraryKeyFileSummary));
        OnPropertyChanged(nameof(HasLibraryPassword));
        OnPropertyChanged(nameof(ShowLibraryPasswordRow));
        OnPropertyChanged(nameof(ShowLibraryKeyRow));
    }

    private static string FormatEndpoint(string username, string host, int port, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(host))
            return placeholder;

        var label = string.IsNullOrWhiteSpace(username) ? host : $"{username}@{host}";
        return port == 22 ? label : $"{label}:{port}";
    }
}
