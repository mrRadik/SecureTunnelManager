using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.ServiceIcons;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Core.Validation;

namespace SecureTunnelManager.UI.ViewModels;

public partial class RdpEditorViewModel : ObservableObject
{
    private readonly IRdpTargetService _targetService;
    private readonly ICredentialService _credentialService;
    private readonly IVaultService _vaultService;

    public RdpEditorViewModel(
        IRdpTargetService targetService,
        ICredentialService credentialService,
        IVaultService vaultService)
    {
        _targetService = targetService;
        _credentialService = credentialService;
        _vaultService = vaultService;
    }

    public int TargetId { get; private set; }
    public bool IsEditMode => TargetId > 0;
    public string WindowTitle => IsEditMode ? "Edit computer" : "New computer";

    [ObservableProperty] private int _currentStep;

    public bool IsFirstStep => CurrentStep == 0;
    public bool IsLastStep => CurrentStep == 2;
    public string CurrentStepTitle => CurrentStep switch
    {
        0 => "Computer",
        1 => "Jump Host",
        2 => "Remote Desktop",
        _ => string.Empty
    };

    public string StepIndicator => $"Step {CurrentStep + 1} of 3";

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _iconKey = ServiceIconCatalog.DefaultRdpKey;
    [ObservableProperty] private string _groupName = string.Empty;

    public ObservableCollection<string> ExistingGroups { get; } = new();

    public ObservableCollection<JumpHostHopViewModel> JumpHosts { get; } = new();

    [ObservableProperty] private string _rdpHost = string.Empty;
    [ObservableProperty] private int _rdpPort = 3389;
    [ObservableProperty] private int _localPort;
    [ObservableProperty] private string _localBindAddress = "127.0.0.1";

    [ObservableProperty] private int? _rdpCredentialId;
    [ObservableProperty] private string _rdpUsername = string.Empty;
    public string RdpPassword { get; set; } = string.Empty;

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _nameError = string.Empty;
    [ObservableProperty] private string _rdpHostError = string.Empty;
    [ObservableProperty] private string _rdpPortError = string.Empty;
    [ObservableProperty] private string _localPortError = string.Empty;
    [ObservableProperty] private string _rdpCredentialError = string.Empty;

    public bool DialogResult { get; private set; }
    public event EventHandler? RequestClose;

    public async Task InitializeAsync(RdpTarget? target)
    {
        ClearValidationErrors();
        RdpPassword = string.Empty;

        ExistingGroups.Clear();
        foreach (var group in await _targetService.GetGroupNamesAsync().ConfigureAwait(true))
            ExistingGroups.Add(group);

        if (target is null)
        {
            TargetId = 0;
            CurrentStep = 0;
            Name = string.Empty;
            Description = string.Empty;
            IconKey = ServiceIconCatalog.DefaultRdpKey;
            GroupName = string.Empty;
            RdpHost = string.Empty;
            RdpPort = 3389;
            LocalPort = 0;
            LocalBindAddress = "127.0.0.1";
            RdpCredentialId = null;
            RdpUsername = string.Empty;
            ResetJumpHosts(new List<JumpHostHop> { new() { Port = 22 } });
        }
        else
        {
            TargetId = target.Id;
            CurrentStep = 0;
            Name = target.Name;
            Description = target.Description;
            IconKey = string.IsNullOrWhiteSpace(target.IconKey) ? ServiceIconCatalog.DefaultRdpKey : target.IconKey;
            GroupName = target.GroupName ?? string.Empty;
            RdpHost = target.RdpHost;
            RdpPort = target.RdpPort;
            LocalPort = target.LocalPort;
            LocalBindAddress = string.IsNullOrWhiteSpace(target.LocalBindAddress) ? "127.0.0.1" : target.LocalBindAddress;
            RdpCredentialId = target.RdpCredentialId;
            ResetJumpHosts(target.JumpHosts.Count > 0 ? target.JumpHosts : new List<JumpHostHop> { new() { Port = 22 } });

            RdpUsername = string.Empty;
            if (target.RdpCredentialId.HasValue)
            {
                var cred = await _credentialService.GetByIdAsync(target.RdpCredentialId.Value).ConfigureAwait(true);
                RdpUsername = cred?.Username ?? string.Empty;
            }
        }

        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IsEditMode));
        NotifyStepPropertiesChanged();
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

        if (CurrentStep < 2)
            CurrentStep++;
    }

    [RelayCommand]
    private void AddJumpHost()
    {
        var hop = CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, JumpHosts.Count);
        JumpHosts.Add(hop);
        RefreshJumpHostIndexes();
    }

    [RelayCommand]
    private void RemoveJumpHost(JumpHostHopViewModel? hop)
    {
        if (hop is null || JumpHosts.Count <= 1)
            return;

        JumpHosts.Remove(hop);
        RefreshJumpHostIndexes();
    }

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

        for (var step = 0; step <= 2; step++)
        {
            if (!ValidateStep(step))
            {
                CurrentStep = step;
                return;
            }
        }

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

            var username = RdpUsername.Trim();
            int? rdpCredentialId = RdpCredentialId;

            if (string.IsNullOrWhiteSpace(username))
            {
                rdpCredentialId = null;
            }
            else
            {
                if (string.IsNullOrEmpty(RdpPassword) && !rdpCredentialId.HasValue)
                {
                    RdpCredentialError = "Password is required";
                    CurrentStep = 2;
                    return;
                }

                rdpCredentialId = await UpsertPasswordCredentialAsync(
                    rdpCredentialId,
                    BuildCredentialName("rdp"),
                    username,
                    RdpPassword).ConfigureAwait(true);
            }

            var model = new RdpTarget
            {
                Id = TargetId,
                Name = Name.Trim(),
                Description = Description.Trim(),
                IconKey = string.IsNullOrWhiteSpace(IconKey) ? ServiceIconCatalog.DefaultRdpKey : IconKey.Trim(),
                GroupName = string.IsNullOrWhiteSpace(GroupName) ? null : GroupName.Trim(),
                JumpHosts = jumpModels,
                RdpHost = RdpHost.Trim(),
                RdpPort = RdpPort,
                RdpCredentialId = rdpCredentialId,
                LocalPort = LocalPort,
                LocalBindAddress = string.IsNullOrWhiteSpace(LocalBindAddress) ? "127.0.0.1" : LocalBindAddress.Trim()
            };

            if (IsEditMode)
                await _targetService.UpdateAsync(model).ConfigureAwait(true);
            else
                await _targetService.CreateAsync(model).ConfigureAwait(true);

            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
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
                if (string.IsNullOrWhiteSpace(RdpHost))
                {
                    RdpHostError = "Host is required";
                    valid = false;
                }
                else if (!NetworkAddressValidator.TryValidateHostOrIp(RdpHost, out var hostError))
                {
                    RdpHostError = hostError;
                    valid = false;
                }

                if (RdpPort is < 1 or > 65535)
                {
                    RdpPortError = "Port must be between 1 and 65535";
                    valid = false;
                }

                if (LocalPort is < 0 or > 65535)
                {
                    LocalPortError = "Use 0 for auto, or 1–65535";
                    valid = false;
                }

                if (!string.IsNullOrWhiteSpace(RdpUsername)
                    && string.IsNullOrEmpty(RdpPassword)
                    && !RdpCredentialId.HasValue)
                {
                    RdpCredentialError = "Password is required when username is set";
                    valid = false;
                }

                if (string.IsNullOrWhiteSpace(RdpUsername) && !string.IsNullOrEmpty(RdpPassword))
                {
                    RdpCredentialError = "Username is required when password is set";
                    valid = false;
                }
                break;
        }

        return valid;
    }

    private void ClearValidationErrors()
    {
        NameError = string.Empty;
        RdpHostError = string.Empty;
        RdpPortError = string.Empty;
        LocalPortError = string.Empty;
        RdpCredentialError = string.Empty;
    }

    private void ResetJumpHosts(IReadOnlyList<JumpHostHop> hops)
    {
        JumpHosts.Clear();
        for (var i = 0; i < hops.Count; i++)
            JumpHosts.Add(CreateJumpHostViewModel(hops[i], i));

        if (JumpHosts.Count == 0)
            JumpHosts.Add(CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, 0));

        RefreshJumpHostIndexes();
    }

    private JumpHostHopViewModel CreateJumpHostViewModel(JumpHostHop hop, int index)
    {
        var vm = JumpHostHopViewModel.FromModel(hop, index);
        return vm;
    }

    private void RefreshJumpHostIndexes()
    {
        for (var i = 0; i < JumpHosts.Count; i++)
        {
            JumpHosts[i].Index = i;
            JumpHosts[i].CanRemove = JumpHosts.Count > 1;
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

        return await _credentialService.CreateAsync(credentialName, "passphrase", secret).ConfigureAwait(true);
    }
}
