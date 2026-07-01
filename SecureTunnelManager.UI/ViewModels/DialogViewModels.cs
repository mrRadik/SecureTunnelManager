using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Core.Validation;
using System.Collections.ObjectModel;

namespace SecureTunnelManager.UI.ViewModels;

public partial class VaultSetupViewModel : ObservableObject
{
    private readonly IVaultService _vaultService;

    public VaultSetupViewModel(IVaultService vaultService) => _vaultService = vaultService;

    [ObservableProperty]
    private string _masterPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    partial void OnMasterPasswordChanged(string value) => ErrorMessage = string.Empty;

    partial void OnConfirmPasswordChanged(string value) => ErrorMessage = string.Empty;

    public bool DialogResult { get; private set; }

    [RelayCommand]
    private async Task CreateAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(MasterPassword) || MasterPassword.Length < 8)
        {
            ErrorMessage = "Master password must be at least 8 characters.";
            return;
        }

        if (MasterPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match. Please re-enter both fields.";
            return;
        }

        try
        {
            await _vaultService.InitializeVaultAsync(MasterPassword).ConfigureAwait(true);
            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public event EventHandler? RequestClose;
}

public partial class UnlockVaultViewModel : ObservableObject
{
    private readonly IVaultService _vaultService;
    private readonly ITunnelManagerService _tunnelManager;

    public UnlockVaultViewModel(IVaultService vaultService, ITunnelManagerService tunnelManager)
    {
        _vaultService = vaultService;
        _tunnelManager = tunnelManager;
    }

    [ObservableProperty]
    private string _masterPassword = string.Empty;

    [ObservableProperty]
    private string _newMasterPassword = string.Empty;

    [ObservableProperty]
    private string _confirmNewPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isResetMode;

    [ObservableProperty]
    private bool _isConfirmResetMode;

    public bool IsUnlockMode => !IsResetMode && !IsConfirmResetMode;

    partial void OnIsResetModeChanged(bool value) => OnPropertyChanged(nameof(IsUnlockMode));

    partial void OnIsConfirmResetModeChanged(bool value) => OnPropertyChanged(nameof(IsUnlockMode));

    partial void OnMasterPasswordChanged(string value) => ErrorMessage = string.Empty;

    public bool DialogResult { get; private set; }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            ErrorMessage = "Password required.";
            return;
        }

        var ok = await _vaultService.UnlockAsync(MasterPassword).ConfigureAwait(true);
        if (!ok)
        {
            ErrorMessage = "Incorrect master password. Please try again.";
            return;
        }

        DialogResult = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestResetVault()
    {
        ErrorMessage = string.Empty;
        MasterPassword = string.Empty;
        IsConfirmResetMode = true;
        OnPropertyChanged(nameof(IsUnlockMode));
    }

    [RelayCommand]
    private void CancelResetConfirm()
    {
        IsConfirmResetMode = false;
        OnPropertyChanged(nameof(IsUnlockMode));
    }

    [RelayCommand]
    private void ConfirmResetVault()
    {
        ErrorMessage = string.Empty;
        NewMasterPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        IsConfirmResetMode = false;
        IsResetMode = true;
        OnPropertyChanged(nameof(IsUnlockMode));
    }

    [RelayCommand]
    private void BackToUnlock()
    {
        ErrorMessage = string.Empty;
        NewMasterPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        IsResetMode = false;
        IsConfirmResetMode = false;
        OnPropertyChanged(nameof(IsUnlockMode));
    }

    [RelayCommand]
    private async Task ResetVaultAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(NewMasterPassword) || NewMasterPassword.Length < 8)
        {
            ErrorMessage = "New master password must be at least 8 characters.";
            return;
        }

        if (NewMasterPassword != ConfirmNewPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        try
        {
            await _tunnelManager.StopAllAsync().ConfigureAwait(true);
            await _vaultService.ResetVaultAsync(NewMasterPassword).ConfigureAwait(true);
            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public event EventHandler? RequestClose;
}

public partial class TunnelEditorViewModel : ObservableObject
{
    private readonly ITunnelProfileService _profileService;
    private readonly ICredentialService _credentialService;
    private readonly IVaultService _vaultService;
    private readonly ISshTunnelTestService _tunnelTestService;

    public TunnelEditorViewModel(
        ITunnelProfileService profileService,
        ICredentialService credentialService,
        IVaultService vaultService,
        ISshTunnelTestService tunnelTestService)
    {
        _profileService = profileService;
        _credentialService = credentialService;
        _vaultService = vaultService;
        _tunnelTestService = tunnelTestService;
    }

    public int ProfileId { get; private set; }
    public bool IsEditMode => ProfileId > 0;
    public string WindowTitle => IsEditMode ? "Edit Tunnel" : "New Tunnel";

    [ObservableProperty] private int _currentStep;

    public bool IsFirstStep => CurrentStep == 0;
    public bool IsLastStep => CurrentStep == 3;
    public string CurrentStepTitle => CurrentStep switch
    {
        0 => "Tunnel information",
        1 => "Jump Host",
        2 => "Target Server",
        3 => "Port Forward",
        _ => string.Empty
    };

    public string StepIndicator => $"Step {CurrentStep + 1} of 4";

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    public ObservableCollection<JumpHostHopViewModel> JumpHosts { get; } = new();
    public ObservableCollection<string> FlowJumpHosts { get; } = new();

    [ObservableProperty] private string _jumpHost = string.Empty;
    [ObservableProperty] private int _jumpPort = 22;
    [ObservableProperty] private string _jumpUsername = string.Empty;
    [ObservableProperty] private AuthMethod _jumpAuthMethod = AuthMethod.Password;
    [ObservableProperty] private int? _jumpCredentialId;
    [ObservableProperty] private string? _jumpPrivateKeyPath;
    [ObservableProperty] private int? _jumpKeyPassphraseCredentialId;

    [ObservableProperty] private string _targetHost = string.Empty;
    [ObservableProperty] private int _targetPort = 22;
    [ObservableProperty] private string _targetUsername = string.Empty;
    [ObservableProperty] private AuthMethod _targetAuthMethod = AuthMethod.Password;
    [ObservableProperty] private int? _targetCredentialId;
    [ObservableProperty] private string? _targetPrivateKeyPath;
    [ObservableProperty] private int? _targetKeyPassphraseCredentialId;

    [ObservableProperty] private string _localBindAddress = "127.0.0.1";
    [ObservableProperty] private int _localPort = 8080;
    [ObservableProperty] private int _remotePort = 80;
    [ObservableProperty] private bool _startWithWindows;

    [ObservableProperty] private string _localBindAddressError = string.Empty;

    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private string _nameError = string.Empty;
    [ObservableProperty] private string _jumpHostError = string.Empty;
    [ObservableProperty] private string _jumpUsernameError = string.Empty;
    [ObservableProperty] private string _jumpCredentialError = string.Empty;
    [ObservableProperty] private string _jumpPrivateKeyError = string.Empty;
    [ObservableProperty] private string _targetHostError = string.Empty;
    [ObservableProperty] private string _targetUsernameError = string.Empty;
    [ObservableProperty] private string _targetCredentialError = string.Empty;
    [ObservableProperty] private string _targetPrivateKeyError = string.Empty;
    [ObservableProperty] private string _localPortError = string.Empty;

    [ObservableProperty] private bool _isTesting;

    [ObservableProperty] private string _testMessage = string.Empty;

    [ObservableProperty] private bool _hasTestResult;

    [ObservableProperty] private bool _testSucceeded;

    /// <summary>Password entered in UI; persisted to vault on save.</summary>
    public string JumpPassword { get; set; } = string.Empty;
    public string TargetPassword { get; set; } = string.Empty;
    public string JumpKeyPassphrase { get; set; } = string.Empty;
    public string TargetKeyPassphrase { get; set; } = string.Empty;

    public bool IsJumpPasswordAuth
    {
        get => JumpAuthMethod == AuthMethod.Password;
        set { if (value) JumpAuthMethod = AuthMethod.Password; }
    }

    public bool IsJumpPrivateKeyAuth
    {
        get => JumpAuthMethod == AuthMethod.PrivateKey;
        set
        {
            if (value)
                JumpAuthMethod = AuthMethod.PrivateKey;
            else if (JumpAuthMethod == AuthMethod.PrivateKey)
                JumpAuthMethod = AuthMethod.Password;
        }
    }

    public bool IsTargetPasswordAuth
    {
        get => TargetAuthMethod == AuthMethod.Password;
        set { if (value) TargetAuthMethod = AuthMethod.Password; }
    }

    public bool IsTargetPrivateKeyAuth
    {
        get => TargetAuthMethod == AuthMethod.PrivateKey;
        set
        {
            if (value)
                TargetAuthMethod = AuthMethod.PrivateKey;
            else if (TargetAuthMethod == AuthMethod.PrivateKey)
                TargetAuthMethod = AuthMethod.Password;
        }
    }

    public string FlowMyComputer => $"{LocalBindAddress}:{LocalPort}";
    public string FlowTargetServer => FormatEndpoint(TargetUsername, TargetHost, TargetPort, "Target Server");
    public string FlowForwardedService => string.IsNullOrWhiteSpace(TargetHost)
        ? $"(target server):{RemotePort}"
        : $"{TargetHost}:{RemotePort}";

    public bool DialogResult { get; private set; }

    public Task InitializeAsync(TunnelProfile? profile)
    {
        ClearValidationErrors();
        JumpPassword = string.Empty;
        TargetPassword = string.Empty;
        JumpKeyPassphrase = string.Empty;
        TargetKeyPassphrase = string.Empty;

        if (profile is null)
        {
            ProfileId = 0;
            CurrentStep = 0;
            LocalBindAddress = "127.0.0.1";
            ResetJumpHosts(new List<JumpHostHop> { new() { Port = 22 } });
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(IsEditMode));
            NotifyStepPropertiesChanged();
            NotifyFlowDiagramChanged();
            return Task.CompletedTask;
        }

        ProfileId = profile.Id;
        CurrentStep = 0;
        Name = profile.Name;
        Description = profile.Description;
        profile.EnsureJumpHostsFromLegacy();
        ResetJumpHosts(profile.JumpHosts);
        TargetHost = profile.TargetHost;
        TargetPort = profile.TargetPort;
        TargetUsername = profile.TargetUsername;
        TargetAuthMethod = profile.TargetAuthMethod;
        TargetCredentialId = profile.TargetCredentialId;
        TargetPrivateKeyPath = profile.TargetPrivateKeyPath;
        TargetKeyPassphraseCredentialId = profile.TargetKeyPassphraseCredentialId;
        LocalBindAddress = string.IsNullOrWhiteSpace(profile.LocalBindAddress) ? "127.0.0.1" : profile.LocalBindAddress;
        LocalPort = profile.LocalPort;
        RemotePort = profile.RemotePort;
        StartWithWindows = profile.StartWithWindows;

        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IsEditMode));
        NotifyAuthPropertiesChanged();
        NotifyStepPropertiesChanged();
        NotifyFlowDiagramChanged();
        return Task.CompletedTask;
    }

    partial void OnCurrentStepChanged(int value) => NotifyStepPropertiesChanged();

    private void NotifyStepPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(StepIndicator));
        BackCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (CurrentStep > 0)
            CurrentStep--;
    }

    private bool CanGoBack() => CurrentStep > 0;

    [RelayCommand]
    private void Next()
    {
        if (!ValidateStep(CurrentStep))
            return;

        if (CurrentStep < 3)
            CurrentStep++;
    }

    private bool ValidateStep(int step)
    {
        ErrorMessage = string.Empty;
        ClearValidationErrors();
        var valid = true;

        switch (step)
        {
            case 0:
                if (string.IsNullOrWhiteSpace(Name))
                {
                    NameError = "Name is required";
                    valid = false;
                }
                break;
            case 1:
                if (JumpHosts.Count == 0)
                {
                    ErrorMessage = "Add at least one jump host.";
                    valid = false;
                    break;
                }

                foreach (var hop in JumpHosts)
                {
                    if (!hop.Validate(IsEditMode))
                        valid = false;
                }
                break;
            case 2:
                if (string.IsNullOrWhiteSpace(TargetHost))
                {
                    TargetHostError = "Host is required";
                    valid = false;
                }
                else if (!NetworkAddressValidator.TryValidateHostOrIp(TargetHost, out var targetHostError))
                {
                    TargetHostError = targetHostError;
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(TargetUsername))
                {
                    TargetUsernameError = "Username is required";
                    valid = false;
                }
                if (TargetAuthMethod == AuthMethod.Password
                    && !TargetCredentialId.HasValue
                    && string.IsNullOrEmpty(TargetPassword))
                {
                    TargetCredentialError = "Password is required";
                    valid = false;
                }
                if (TargetAuthMethod == AuthMethod.PrivateKey && string.IsNullOrWhiteSpace(TargetPrivateKeyPath))
                {
                    TargetPrivateKeyError = "Private key file is required";
                    valid = false;
                }
                break;
            case 3:
                if (LocalPort is < 1 or > 65535)
                {
                    LocalPortError = "Port must be between 1 and 65535";
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(LocalBindAddress))
                {
                    LocalBindAddressError = "Local address is required";
                    valid = false;
                }
                else if (!NetworkAddressValidator.TryValidateIpAddress(LocalBindAddress, out var bindError))
                {
                    LocalBindAddressError = bindError;
                    valid = false;
                }
                if (RemotePort is < 1 or > 65535)
                {
                    ErrorMessage = "Service port must be between 1 and 65535";
                    valid = false;
                }
                break;
        }

        if (!valid)
            ErrorMessage = "Please complete the required fields before continuing.";

        return valid;
    }

    partial void OnJumpAuthMethodChanged(AuthMethod value)
    {
        NotifyAuthPropertiesChanged();
        NotifyFlowDiagramChanged();
    }

    partial void OnTargetAuthMethodChanged(AuthMethod value)
    {
        NotifyAuthPropertiesChanged();
        NotifyFlowDiagramChanged();
    }

    partial void OnNameChanged(string value) => NameError = string.Empty;
    partial void OnJumpHostChanged(string value)
    {
        JumpHostError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnJumpPortChanged(int value) => NotifyFlowDiagramChanged();
    partial void OnJumpUsernameChanged(string value)
    {
        JumpUsernameError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnTargetHostChanged(string value)
    {
        TargetHostError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnTargetPortChanged(int value) => NotifyFlowDiagramChanged();
    partial void OnTargetUsernameChanged(string value)
    {
        TargetUsernameError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnLocalBindAddressChanged(string value)
    {
        LocalBindAddressError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnLocalPortChanged(int value)
    {
        LocalPortError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnRemotePortChanged(int value) => NotifyFlowDiagramChanged();

    [RelayCommand]
    private void BrowseJumpKey()
    {
        var path = PickFile();
        if (path is not null)
        {
            JumpPrivateKeyPath = path;
            JumpPrivateKeyError = string.Empty;
        }
    }

    [RelayCommand]
    private void BrowseTargetKey()
    {
        var path = PickFile();
        if (path is not null)
        {
            TargetPrivateKeyPath = path;
            TargetPrivateKeyError = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTestTunnel))]
    private async Task TestTunnelAsync()
    {
        ErrorMessage = string.Empty;
        TestMessage = string.Empty;
        HasTestResult = false;
        _vaultService.NotifyActivity();

        for (var step = 0; step <= 3; step++)
        {
            if (!ValidateStep(step))
            {
                CurrentStep = step;
                return;
            }
        }

        if (!Validate())
            return;

        IsTesting = true;
        try
        {
            var result = await _tunnelTestService.TestAsync(BuildTestRequest()).ConfigureAwait(true);
            TestSucceeded = result.Success;
            TestMessage = result.Message;
            HasTestResult = true;

            if (!result.Success)
                ErrorMessage = result.Message;
        }
        catch (Exception ex)
        {
            TestSucceeded = false;
            TestMessage = ex.Message;
            HasTestResult = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTesting = false;
        }
    }

    private bool CanTestTunnel() => !IsTesting;

    public string TestTunnelButtonText => IsTesting ? "Testing..." : "Test Tunnel";

    partial void OnIsTestingChanged(bool value)
    {
        TestTunnelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(TestTunnelButtonText));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        _vaultService.NotifyActivity();

        for (var step = 0; step <= 3; step++)
        {
            if (!ValidateStep(step))
            {
                CurrentStep = step;
                return;
            }
        }

        if (!Validate())
            return;

        try
        {
            var jumpModels = new List<JumpHostHop>();
            for (var i = 0; i < JumpHosts.Count; i++)
            {
                var hopVm = JumpHosts[i];
                if (hopVm.AuthMethod == AuthMethod.Password)
                {
                    hopVm.CredentialId = await UpsertPasswordCredentialAsync(
                        hopVm.CredentialId,
                        BuildCredentialName($"jump-{i + 1}"),
                        hopVm.Username,
                        hopVm.Password).ConfigureAwait(true);
                    hopVm.KeyPassphraseCredentialId = null;
                    hopVm.PrivateKeyPath = null;
                }
                else
                {
                    hopVm.CredentialId = null;
                    hopVm.KeyPassphraseCredentialId = await UpsertOptionalSecretAsync(
                        hopVm.KeyPassphraseCredentialId,
                        BuildCredentialName($"jump-{i + 1}-passphrase"),
                        hopVm.KeyPassphrase).ConfigureAwait(true);
                }

                jumpModels.Add(hopVm.ToModel());
            }

            if (TargetAuthMethod == AuthMethod.Password)
            {
                TargetCredentialId = await UpsertPasswordCredentialAsync(
                    TargetCredentialId,
                    BuildCredentialName("target"),
                    TargetUsername,
                    TargetPassword).ConfigureAwait(true);
            }
            else
            {
                TargetCredentialId = null;
                TargetKeyPassphraseCredentialId = await UpsertOptionalSecretAsync(
                    TargetKeyPassphraseCredentialId,
                    BuildCredentialName("target-passphrase"),
                    TargetKeyPassphrase).ConfigureAwait(true);
            }

            var profile = new TunnelProfile
            {
                Id = ProfileId,
                Name = Name.Trim(),
                Description = Description.Trim(),
                JumpHosts = jumpModels,
                TargetHost = TargetHost.Trim(),
                TargetPort = TargetPort,
                TargetUsername = TargetUsername.Trim(),
                TargetAuthMethod = TargetAuthMethod,
                TargetCredentialId = TargetCredentialId,
                TargetPrivateKeyPath = TargetPrivateKeyPath,
                TargetKeyPassphraseCredentialId = TargetKeyPassphraseCredentialId,
                LocalBindAddress = LocalBindAddress.Trim(),
                LocalPort = LocalPort,
                RemoteHost = TargetHost.Trim(),
                RemotePort = RemotePort,
                StartWithWindows = StartWithWindows
            };

            profile.SyncLegacyFieldsFromFirstHop();

            if (IsEditMode)
                await _profileService.UpdateAsync(profile).ConfigureAwait(true);
            else
                await _profileService.CreateAsync(profile).ConfigureAwait(true);

            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private string BuildCredentialName(string suffix) => $"{Name.Trim()}/{suffix}";

    private async Task<int> UpsertPasswordCredentialAsync(
        int? existingId,
        string credentialName,
        string username,
        string password)
    {
        if (existingId.HasValue)
        {
            await _credentialService.UpdateAsync(
                existingId.Value,
                credentialName,
                username,
                string.IsNullOrEmpty(password) ? null : password).ConfigureAwait(true);
            return existingId.Value;
        }

        return await _credentialService.CreateAsync(credentialName, username, password).ConfigureAwait(true);
    }

    private async Task<int?> UpsertOptionalSecretAsync(int? existingId, string credentialName, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return existingId;

        if (existingId.HasValue)
        {
            await _credentialService.UpdateAsync(existingId.Value, credentialName, "passphrase", secret).ConfigureAwait(true);
            return existingId;
        }

        var id = await _credentialService.CreateAsync(credentialName, "passphrase", secret).ConfigureAwait(true);
        return id;
    }

    private bool Validate()
    {
        ClearValidationErrors();
        var valid = true;

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = "Name is required";
            valid = false;
        }

        if (JumpHosts.Count == 0)
            valid = false;

        foreach (var hop in JumpHosts)
        {
            if (!hop.Validate(IsEditMode))
                valid = false;
        }

        if (string.IsNullOrWhiteSpace(TargetHost))
        {
            TargetHostError = "Host is required";
            valid = false;
        }
        else if (!NetworkAddressValidator.TryValidateHostOrIp(TargetHost, out var targetHostError))
        {
            TargetHostError = targetHostError;
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(TargetUsername))
        {
            TargetUsernameError = "Username is required";
            valid = false;
        }

        if (TargetAuthMethod == AuthMethod.Password
            && !TargetCredentialId.HasValue
            && string.IsNullOrEmpty(TargetPassword))
        {
            TargetCredentialError = "Password is required";
            valid = false;
        }

        if (TargetAuthMethod == AuthMethod.PrivateKey && string.IsNullOrWhiteSpace(TargetPrivateKeyPath))
        {
            TargetPrivateKeyError = "Private key path is required";
            valid = false;
        }

        if (LocalPort is < 1 or > 65535)
        {
            LocalPortError = "Port must be between 1 and 65535";
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(LocalBindAddress))
        {
            LocalBindAddressError = "Bind address is required";
            valid = false;
        }
        else if (!NetworkAddressValidator.TryValidateIpAddress(LocalBindAddress, out var bindError))
        {
            LocalBindAddressError = bindError;
            valid = false;
        }

        if (RemotePort is < 1 or > 65535)
        {
            valid = false;
            ErrorMessage = "Forwarded port must be between 1 and 65535";
        }

        if (!valid)
            ErrorMessage = "Please fix the highlighted fields before saving.";

        return valid;
    }

    private void ClearValidationErrors()
    {
        NameError = string.Empty;
        JumpHostError = string.Empty;
        JumpUsernameError = string.Empty;
        JumpCredentialError = string.Empty;
        JumpPrivateKeyError = string.Empty;
        TargetHostError = string.Empty;
        TargetUsernameError = string.Empty;
        TargetCredentialError = string.Empty;
        TargetPrivateKeyError = string.Empty;
        LocalPortError = string.Empty;
        LocalBindAddressError = string.Empty;
    }

    private void NotifyFlowDiagramChanged()
    {
        OnPropertyChanged(nameof(FlowMyComputer));
        OnPropertyChanged(nameof(FlowTargetServer));
        OnPropertyChanged(nameof(FlowForwardedService));

        FlowJumpHosts.Clear();
        foreach (var hop in JumpHosts)
            FlowJumpHosts.Add(hop.FlowDisplay);
    }

    [RelayCommand]
    private void AddJumpHost()
    {
        var hop = CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, JumpHosts.Count);
        JumpHosts.Add(hop);
        UpdateJumpHostIndices();
        NotifyFlowDiagramChanged();
    }

    [RelayCommand]
    private void RemoveJumpHost(JumpHostHopViewModel? hop)
    {
        if (hop is null || JumpHosts.Count <= 1)
            return;

        JumpHosts.Remove(hop);
        UpdateJumpHostIndices();
        NotifyFlowDiagramChanged();
    }

    private void ResetJumpHosts(IReadOnlyList<JumpHostHop> hops)
    {
        JumpHosts.Clear();
        for (var i = 0; i < hops.Count; i++)
            JumpHosts.Add(CreateJumpHostViewModel(hops[i], i));

        if (JumpHosts.Count == 0)
            JumpHosts.Add(CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, 0));

        UpdateJumpHostIndices();
    }

    private JumpHostHopViewModel CreateJumpHostViewModel(JumpHostHop hop, int index)
    {
        var vm = JumpHostHopViewModel.FromModel(hop, index);
        vm.FlowChanged += (_, _) => NotifyFlowDiagramChanged();
        return vm;
    }

    private void UpdateJumpHostIndices()
    {
        for (var i = 0; i < JumpHosts.Count; i++)
        {
            JumpHosts[i].Index = i;
            JumpHosts[i].CanRemove = JumpHosts.Count > 1;
        }
    }

    private static string FormatEndpoint(string username, string host, int port, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(host))
            return placeholder;

        var label = string.IsNullOrWhiteSpace(username) ? host : $"{username}@{host}";
        return port == 22 ? label : $"{label}:{port}";
    }

    private void NotifyAuthPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsJumpPasswordAuth));
        OnPropertyChanged(nameof(IsJumpPrivateKeyAuth));
        OnPropertyChanged(nameof(IsTargetPasswordAuth));
        OnPropertyChanged(nameof(IsTargetPrivateKeyAuth));
    }

    private static string? PickFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Private keys (*.pem;*.ppk;*.*)|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private TunnelProfile BuildDraftProfile()
    {
        var profile = new TunnelProfile
        {
            Id = ProfileId,
            Name = string.IsNullOrWhiteSpace(Name) ? "test" : Name.Trim(),
            Description = Description.Trim(),
            JumpHosts = JumpHosts.Select(h => h.ToModel()).ToList(),
            TargetHost = TargetHost.Trim(),
            TargetPort = TargetPort,
            TargetUsername = TargetUsername.Trim(),
            TargetAuthMethod = TargetAuthMethod,
            TargetCredentialId = TargetCredentialId,
            TargetPrivateKeyPath = TargetPrivateKeyPath,
            TargetKeyPassphraseCredentialId = TargetKeyPassphraseCredentialId,
            LocalBindAddress = LocalBindAddress.Trim(),
            LocalPort = LocalPort,
            RemoteHost = TargetHost.Trim(),
            RemotePort = RemotePort,
            StartWithWindows = StartWithWindows
        };

        profile.SyncLegacyFieldsFromFirstHop();
        return profile;
    }

    private TunnelTestRequest BuildTestRequest() => new()
    {
        Profile = BuildDraftProfile(),
        JumpAuthOverrides = JumpHosts.Select(h => new TunnelAuthOverride
        {
            Password = string.IsNullOrEmpty(h.Password) ? null : h.Password,
            KeyPassphrase = string.IsNullOrEmpty(h.KeyPassphrase) ? null : h.KeyPassphrase
        }).ToList(),
        TargetAuthOverride = new TunnelAuthOverride
        {
            Password = string.IsNullOrEmpty(TargetPassword) ? null : TargetPassword,
            KeyPassphrase = string.IsNullOrEmpty(TargetKeyPassphrase) ? null : TargetKeyPassphrase
        }
    };

    public event EventHandler? RequestClose;
}
