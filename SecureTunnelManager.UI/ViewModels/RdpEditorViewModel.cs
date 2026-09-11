using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.ServiceIcons;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Core.Validation;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class RdpEditorViewModel : ObservableObject
{
    private readonly IRdpTargetService _targetService;
    private readonly ICredentialService _credentialService;
    private readonly IJumpHostService _jumpHostService;
    private readonly IVaultService _vaultService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;

    public RdpEditorViewModel(
        IRdpTargetService targetService,
        ICredentialService credentialService,
        IJumpHostService jumpHostService,
        IVaultService vaultService,
        ILocalizationService localization,
        IDialogService dialogService)
    {
        _targetService = targetService;
        _credentialService = credentialService;
        _jumpHostService = jumpHostService;
        _vaultService = vaultService;
        _localization = localization;
        _dialogService = dialogService;
        _localization.LanguageChanged += (_, _) => RefreshLocalizedText();
    }

    public int TargetId { get; private set; }
    public bool IsEditMode => TargetId > 0;
    public string WindowTitle => _localization.Get(IsEditMode
        ? "Rdp.Editor.EditTitle"
        : "Rdp.Editor.NewTitle");

    private int? _initialRdpCredentialId;

    [ObservableProperty] private int _currentStep;

    public bool IsFirstStep => CurrentStep == 0;
    public bool IsLastStep => CurrentStep == 2;
    public string CurrentStepTitle => CurrentStep switch
    {
        0 => _localization.Get("Rdp.Editor.StepComputer"),
        1 => _localization.Get("Rdp.Editor.StepJumpHosts"),
        2 => _localization.Get("Rdp.Editor.StepRemoteDesktop"),
        _ => string.Empty
    };

    public string StepIndicator => _localization.Format("Editor.StepIndicator", CurrentStep + 1, 3);

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _iconKey = ServiceIconCatalog.DefaultRdpKey;
    [ObservableProperty] private string _groupName = string.Empty;

    public ObservableCollection<string> ExistingGroups { get; } = new();

    public ObservableCollection<JumpHostHopViewModel> JumpHosts { get; } = new();
    public ObservableCollection<JumpHost> SavedJumpHosts { get; } = new();
    public ObservableCollection<string> FlowJumpHosts { get; } = new();

    [ObservableProperty] private bool _useJumpHost = true;

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

    public string JumpHostsIntro => UseJumpHost
        ? _localization.Get("Rdp.Editor.JumpHostsIntro")
        : _localization.Get("Rdp.Editor.DirectConnectionIntro");

    public string RemoteDesktopIntro => UseJumpHost
        ? _localization.Get("Rdp.Editor.RemoteDesktopIntro")
        : _localization.Get("Rdp.Editor.RemoteDesktopDirectIntro");

    public string FlowMyComputer => !UseJumpHost
        ? _localization.Get("Rdp.Editor.DirectConnection")
        : LocalPort == 0
            ? $"{LocalBindAddress}:auto"
            : $"{LocalBindAddress}:{LocalPort}";

    public string FlowRdpServer => FormatRdpEndpoint(
        RdpUsername,
        RdpHost,
        RdpPort,
        _localization.Get("Editor.RdpServer"));

    public async Task InitializeAsync(RdpTarget? target)
    {
        ClearValidationErrors();
        RdpPassword = string.Empty;

        ExistingGroups.Clear();
        foreach (var group in await _targetService.GetGroupNamesAsync().ConfigureAwait(true))
            ExistingGroups.Add(group);

        await RefreshSavedJumpHostsAsync().ConfigureAwait(true);

        if (target is null)
        {
            TargetId = 0;
            _initialRdpCredentialId = null;
            CurrentStep = 0;
            Name = string.Empty;
            Description = string.Empty;
            IconKey = ServiceIconCatalog.DefaultRdpKey;
            GroupName = string.Empty;
            UseJumpHost = true;
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
            _initialRdpCredentialId = target.RdpCredentialId;
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
            UseJumpHost = target.JumpHosts.Count > 0;
            ResetJumpHosts(UseJumpHost ? target.JumpHosts : Array.Empty<JumpHostHop>());
            BindJumpHostLibrarySelections();

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
        NotifyFlowDiagramChanged();
    }

    partial void OnRdpHostChanged(string value)
    {
        RdpHostError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    partial void OnRdpPortChanged(int value) => NotifyFlowDiagramChanged();

    partial void OnLocalPortChanged(int value) => NotifyFlowDiagramChanged();

    partial void OnLocalBindAddressChanged(string value) => NotifyFlowDiagramChanged();

    partial void OnUseJumpHostChanged(bool value)
    {
        ErrorMessage = string.Empty;
        if (value && JumpHosts.Count == 0)
            JumpHosts.Add(CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, 0));

        RefreshJumpHostIndexes();
        OnPropertyChanged(nameof(JumpHostsIntro));
        OnPropertyChanged(nameof(RemoteDesktopIntro));
        NotifyFlowDiagramChanged();
    }

    partial void OnRdpUsernameChanged(string value)
    {
        RdpCredentialError = string.Empty;
        NotifyFlowDiagramChanged();
    }

    private bool HasStoredRdpCredential =>
        RdpCredentialId.HasValue || (IsEditMode && _initialRdpCredentialId.HasValue);

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

    [RelayCommand(CanExecute = nameof(UseJumpHost))]
    private void AddJumpHost()
    {
        var hop = CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, JumpHosts.Count);
        JumpHosts.Add(hop);
        RefreshJumpHostIndexes();
        NotifyFlowDiagramChanged();
    }

    [RelayCommand]
    private async Task AddNewJumpHostAsync(JumpHostHopViewModel? hop)
    {
        var created = await _dialogService.ShowJumpHostEditorAsync().ConfigureAwait(true);
        if (created is null)
            return;

        await RefreshSavedJumpHostsAsync().ConfigureAwait(true);
        if (hop is not null)
        {
            var saved = SavedJumpHosts.FirstOrDefault(j => j.Id == created.Id);
            if (saved is not null)
                hop.BindSavedJumpHost(saved);
        }
    }

    private async Task RefreshSavedJumpHostsAsync()
    {
        SavedJumpHosts.Clear();
        foreach (var jumpHost in await _jumpHostService.GetAllAsync().ConfigureAwait(true))
            SavedJumpHosts.Add(jumpHost);

        foreach (var hop in JumpHosts)
            hop.RefreshSavedJumpHostFromList(SavedJumpHosts);
    }

    private void BindJumpHostLibrarySelections()
    {
        foreach (var hop in JumpHosts)
        {
            hop.TryBindPendingSavedJumpHost(SavedJumpHosts);
            hop.RefreshSavedJumpHostFromList(SavedJumpHosts);
            if (hop.SelectedSavedJumpHost is null && SavedJumpHosts.Count > 0)
                hop.BindSavedJumpHost(SavedJumpHosts[0]);
        }
    }

    [RelayCommand]
    private void RemoveJumpHost(JumpHostHopViewModel? hop)
    {
        if (hop is null || JumpHosts.Count <= 1)
            return;

        JumpHosts.Remove(hop);
        RefreshJumpHostIndexes();
        NotifyFlowDiagramChanged();
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
            if (UseJumpHost)
            {
                foreach (var hopVm in JumpHosts)
                    jumpModels.Add(hopVm.ToModel());
            }

            var username = RdpUsername.Trim();
            var rdpCredentialId = RdpCredentialId ?? (IsEditMode ? _initialRdpCredentialId : null);

            if (string.IsNullOrWhiteSpace(username))
            {
                rdpCredentialId = null;
            }
            else if (string.IsNullOrEmpty(RdpPassword) && !rdpCredentialId.HasValue)
            {
                RdpCredentialError = _localization.Get("Editor.Validation.PasswordRequired");
                CurrentStep = 2;
                return;
            }
            else if (!string.IsNullOrEmpty(RdpPassword) || rdpCredentialId.HasValue)
            {
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
            var current = ex;
            while (current.InnerException is not null)
                current = current.InnerException;
            ErrorMessage = string.IsNullOrWhiteSpace(current.Message) ? ex.Message : current.Message;
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
                    NameError = _localization.Get("Editor.Validation.NameRequired");
                    valid = false;
                }
                break;
            case 1:
                if (!UseJumpHost)
                    break;

                if (JumpHosts.Count == 0)
                {
                    ErrorMessage = _localization.Get("Editor.Validation.JumpHostRequired");
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
                    RdpHostError = _localization.Get("Editor.Validation.HostRequired");
                    valid = false;
                }
                else if (!NetworkAddressValidator.IsValidHostOrIp(RdpHost))
                {
                    RdpHostError = GetInvalidHostMessage(RdpHost);
                    valid = false;
                }

                if (RdpPort is < 1 or > 65535)
                {
                    RdpPortError = _localization.Get("Editor.Validation.PortRange");
                    valid = false;
                }

                if (UseJumpHost && LocalPort is < 0 or > 65535)
                {
                    LocalPortError = _localization.Get("Rdp.Editor.Validation.LocalPortRange");
                    valid = false;
                }

                if (!string.IsNullOrWhiteSpace(RdpUsername)
                    && string.IsNullOrEmpty(RdpPassword)
                    && !HasStoredRdpCredential)
                {
                    RdpCredentialError = _localization.Get("Rdp.Editor.Validation.PasswordWithUsername");
                    valid = false;
                }

                if (string.IsNullOrWhiteSpace(RdpUsername) && !string.IsNullOrEmpty(RdpPassword))
                {
                    RdpCredentialError = _localization.Get("Rdp.Editor.Validation.UsernameWithPassword");
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

        if (UseJumpHost && JumpHosts.Count == 0)
            JumpHosts.Add(CreateJumpHostViewModel(new JumpHostHop { Port = 22 }, 0));

        RefreshJumpHostIndexes();
        AddJumpHostCommand.NotifyCanExecuteChanged();
    }

    private JumpHostHopViewModel CreateJumpHostViewModel(JumpHostHop hop, int index)
    {
        var vm = JumpHostHopViewModel.FromModel(hop, index, _localization);
        vm.SetSavedJumpHostsLookup(() => SavedJumpHosts);
        vm.SetCredentialPasswordLoader(LoadCredentialPasswordAsync);
        vm.FlowChanged += (_, _) => NotifyFlowDiagramChanged();
        vm.EditSavedJumpHostHandler = EditSavedJumpHostAsync;
        vm.DeleteSavedJumpHostHandler = DeleteSavedJumpHostFromHopAsync;
        return vm;
    }

    private async Task<string?> LoadCredentialPasswordAsync(int credentialId)
    {
        if (!_vaultService.IsUnlocked)
            return null;

        return await _credentialService.GetPasswordAsync(credentialId).ConfigureAwait(false);
    }

    private async Task DeleteSavedJumpHostFromHopAsync(JumpHostHopViewModel hop)
    {
        if (hop.SelectedSavedJumpHost is not JumpHost jumpHost)
            return;

        var refCounts = await _jumpHostService.GetReferenceCountsAsync().ConfigureAwait(true);
        refCounts.TryGetValue(jumpHost.Id, out var refCount);

        var message = refCount > 0
            ? _localization.Format("JumpHosts.DeleteConfirmInUse", jumpHost.Name, refCount)
            : _localization.Format("JumpHosts.DeleteConfirm", jumpHost.Name);

        if (!_dialogService.ShowConfirm(message, _localization.Get("JumpHosts.DeleteTitle"), destructiveConfirm: true))
            return;

        try
        {
            await _jumpHostService.DeleteAsync(jumpHost.Id).ConfigureAwait(true);
            await RefreshSavedJumpHostsAsync().ConfigureAwait(true);
            hop.ClearLibrarySelection();

            if (SavedJumpHosts.Count > 0)
                hop.BindSavedJumpHost(SavedJumpHosts[0]);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task<JumpHost?> EditSavedJumpHostAsync(JumpHost existing)
    {
        var updated = await _dialogService.ShowJumpHostEditorAsync(existing).ConfigureAwait(true);
        if (updated is null)
            return null;

        await RefreshSavedJumpHostsAsync().ConfigureAwait(true);
        return SavedJumpHosts.FirstOrDefault(j => j.Id == updated.Id) ?? updated;
    }

    private void NotifyFlowDiagramChanged()
    {
        OnPropertyChanged(nameof(FlowMyComputer));
        OnPropertyChanged(nameof(FlowRdpServer));

        FlowJumpHosts.Clear();
        if (UseJumpHost)
        {
            foreach (var hop in JumpHosts)
                FlowJumpHosts.Add(hop.FlowDisplay);
        }
    }

    private static string FormatEndpoint(string username, string host, int port, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(host))
            return placeholder;

        var label = string.IsNullOrWhiteSpace(username) ? host : $"{username}@{host}";
        return port == 22 ? label : $"{label}:{port}";
    }

    private static string FormatRdpEndpoint(string username, string host, int port, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(host))
            return placeholder;

        var label = string.IsNullOrWhiteSpace(username) ? host : $"{username}@{host}";
        return $"{label}:{port}";
    }

    private void RefreshJumpHostIndexes()
    {
        for (var i = 0; i < JumpHosts.Count; i++)
        {
            JumpHosts[i].Index = i;
            JumpHosts[i].CanRemove = JumpHosts.Count > 1;
        }
    }

    private string GetInvalidHostMessage(string value) =>
        _localization.Get(NetworkAddressValidator.IsIpFormatAttempt(value)
            ? "Editor.Validation.InvalidIpAddress"
            : "Editor.Validation.InvalidHost");

    private void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(StepIndicator));
        OnPropertyChanged(nameof(JumpHostsIntro));
        OnPropertyChanged(nameof(RemoteDesktopIntro));
        foreach (var hop in JumpHosts)
            hop.RefreshLocalizedText();
        NotifyFlowDiagramChanged();
    }

    private string BuildCredentialName(string suffix) => $"{Name.Trim()}/{suffix}";

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
