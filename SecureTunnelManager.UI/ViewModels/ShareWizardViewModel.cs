using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class ShareWizardViewModel : ObservableObject
{
    private readonly ITunnelProfileService _tunnelProfileService;
    private readonly IRdpTargetService _rdpTargetService;
    private readonly IExportImportService _exportImportService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;

    private ConnectionShareBundle? _importBundle;

    public ShareWizardViewModel(
        ITunnelProfileService tunnelProfileService,
        IRdpTargetService rdpTargetService,
        IExportImportService exportImportService,
        ILocalizationService localization,
        IDialogService dialogService,
        INotificationService notificationService)
    {
        _tunnelProfileService = tunnelProfileService;
        _rdpTargetService = rdpTargetService;
        _exportImportService = exportImportService;
        _localization = localization;
        _dialogService = dialogService;
        _notificationService = notificationService;
    }

    public ObservableCollection<ShareConnectionItemViewModel> ExportTunnelItems { get; } = new();

    public ObservableCollection<ShareConnectionItemViewModel> ExportRdpItems { get; } = new();

    public ObservableCollection<ShareConnectionItemViewModel> ImportTunnelItems { get; } = new();

    public ObservableCollection<ShareConnectionItemViewModel> ImportRdpItems { get; } = new();

    [ObservableProperty] private bool _isExportMode = true;

    public bool IsImportMode
    {
        get => !IsExportMode;
        set => IsExportMode = !value;
    }

    [ObservableProperty] private string _importFileName = string.Empty;

    [ObservableProperty] private string _importSummary = string.Empty;

    [ObservableProperty] private string _exportTunnelSectionTitle = string.Empty;

    [ObservableProperty] private string _exportRdpSectionTitle = string.Empty;

    [ObservableProperty] private string _importTunnelSectionTitle = string.Empty;

    [ObservableProperty] private string _importRdpSectionTitle = string.Empty;

    [ObservableProperty] private bool _hasImportPreview;

    [ObservableProperty] private bool _hasExportTunnelItems;

    [ObservableProperty] private bool _hasExportRdpItems;

    [ObservableProperty] private bool _hasImportTunnelItems;

    [ObservableProperty] private bool _hasImportRdpItems;

    [ObservableProperty] private bool _isBusy;

    public bool DialogResult { get; private set; }

    public ShareImportResult? ImportResult { get; private set; }

    public event EventHandler? RequestClose;

    public bool HasExportItems => HasExportTunnelItems || HasExportRdpItems;

    private bool CanImport() => HasImportPreview && !IsBusy;

    public async Task InitializeAsync()
    {
        ExportTunnelItems.Clear();
        ExportRdpItems.Clear();
        ImportTunnelItems.Clear();
        ImportRdpItems.Clear();
        HasImportPreview = false;
        ImportFileName = string.Empty;
        ImportSummary = string.Empty;
        _importBundle = null;

        var tunnels = await _tunnelProfileService.GetAllAsync().ConfigureAwait(true);
        foreach (var tunnel in tunnels.OrderBy(t => t.Name))
        {
            ExportTunnelItems.Add(new ShareConnectionItemViewModel
            {
                Kind = ShareConnectionKind.Tunnel,
                ResourceId = tunnel.Id,
                Name = tunnel.Name,
                Subtitle = FormatTunnelSubtitle(tunnel.LocalBindAddress, tunnel.LocalPort, tunnel.RemoteHost, tunnel.RemotePort),
                IsSelected = true
            });
        }

        var rdpTargets = await _rdpTargetService.GetAllAsync().ConfigureAwait(true);
        foreach (var target in rdpTargets.OrderBy(t => t.Name))
        {
            ExportRdpItems.Add(new ShareConnectionItemViewModel
            {
                Kind = ShareConnectionKind.Rdp,
                ResourceId = target.Id,
                Name = target.Name,
                Subtitle = FormatRdpSubtitle(target.RdpHost, target.RdpPort, target.GroupName),
                IsSelected = true
            });
        }

        RefreshSectionTitles();
    }

    partial void OnIsExportModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsImportMode));
        ExportCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        BrowseImportCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasImportPreviewChanged(bool value) =>
        ImportCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
    {
        ExportCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        BrowseImportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectAllExport()
    {
        foreach (var item in ExportTunnelItems)
            item.IsSelected = true;
        foreach (var item in ExportRdpItems)
            item.IsSelected = true;
    }

    [RelayCommand]
    private void ClearExportSelection()
    {
        foreach (var item in ExportTunnelItems)
            item.IsSelected = false;
        foreach (var item in ExportRdpItems)
            item.IsSelected = false;
    }

    [RelayCommand]
    private void SelectAllImport()
    {
        foreach (var item in ImportTunnelItems)
            item.IsSelected = true;
        foreach (var item in ImportRdpItems)
            item.IsSelected = true;
        RefreshSectionTitles();
    }

    [RelayCommand]
    private void ClearImportSelection()
    {
        foreach (var item in ImportTunnelItems)
            item.IsSelected = false;
        foreach (var item in ImportRdpItems)
            item.IsSelected = false;
        RefreshSectionTitles();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteExport))]
    private async Task ExportAsync()
    {
        var selectedTunnels = ExportTunnelItems.Where(i => i.IsSelected).ToList();
        var selectedRdp = ExportRdpItems.Where(i => i.IsSelected).ToList();
        if (selectedTunnels.Count == 0 && selectedRdp.Count == 0)
        {
            _dialogService.ShowError(_localization.Get("Share.NothingSelected"));
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = _localization.Get("Share.FileFilter"),
            FileName = "connections.stm"
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            await _exportImportService.ExportConnectionsAsync(
                selectedTunnels.Select(i => i.ResourceId).ToList(),
                selectedRdp.Select(i => i.ResourceId).ToList(),
                dialog.FileName).ConfigureAwait(true);

            _notificationService.Publish(new AppNotification
            {
                Severity = NotificationSeverity.Success,
                MessageKey = "Notification.ShareExportSuccess",
                MessageArgs = [selectedTunnels.Count, selectedRdp.Count]
            });

            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(_localization.Format("Share.ExportFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteBrowse))]
    private async Task BrowseImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = _localization.Get("Share.FileFilter")
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            _importBundle = await _exportImportService.ReadBundleFromFileAsync(dialog.FileName).ConfigureAwait(true);
            ImportTunnelItems.Clear();
            ImportRdpItems.Clear();

            var tunnelOrder = _importBundle.Tunnels
                .Select((tunnel, index) => (tunnel, index))
                .OrderBy(x => x.tunnel.Name)
                .ToList();
            foreach (var (tunnel, index) in tunnelOrder)
            {
                ImportTunnelItems.Add(CreateImportItem(
                    ShareConnectionKind.Tunnel,
                    index,
                    tunnel.Name,
                    FormatTunnelSubtitle(tunnel.LocalBindAddress, tunnel.LocalPort, tunnel.RemoteHost, tunnel.RemotePort)));
            }

            var rdpOrder = _importBundle.RdpTargets
                .Select((target, index) => (target, index))
                .OrderBy(x => x.target.Name)
                .ToList();
            foreach (var (target, index) in rdpOrder)
            {
                ImportRdpItems.Add(CreateImportItem(
                    ShareConnectionKind.Rdp,
                    index,
                    target.Name,
                    FormatRdpSubtitle(target.RdpHost, target.RdpPort, target.GroupName)));
            }

            ImportFileName = System.IO.Path.GetFileName(dialog.FileName);
            HasImportPreview = ImportTunnelItems.Count + ImportRdpItems.Count > 0;
            RefreshSectionTitles();

            if (!HasImportPreview)
                _dialogService.ShowError(_localization.Get("Share.ImportEmpty"));
        }
        catch (Exception ex)
        {
            _importBundle = null;
            HasImportPreview = false;
            ImportFileName = string.Empty;
            ImportSummary = string.Empty;
            ImportTunnelItems.Clear();
            ImportRdpItems.Clear();
            RefreshSectionTitles();
            _dialogService.ShowError(_localization.Format("Share.ImportFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        if (_importBundle is null || !HasImportPreview)
            return;

        var selectedTunnels = ImportTunnelItems.Where(i => i.IsSelected).ToList();
        var selectedRdp = ImportRdpItems.Where(i => i.IsSelected).ToList();
        if (selectedTunnels.Count == 0 && selectedRdp.Count == 0)
        {
            _dialogService.ShowError(_localization.Get("Share.NothingSelected"));
            return;
        }

        var bundle = new ConnectionShareBundle();
        foreach (var item in selectedTunnels)
            bundle.Tunnels.Add(_importBundle.Tunnels[item.BundleIndex]);
        foreach (var item in selectedRdp)
            bundle.RdpTargets.Add(_importBundle.RdpTargets[item.BundleIndex]);

        IsBusy = true;
        try
        {
            ImportResult = await _exportImportService.ImportConnectionsAsync(bundle).ConfigureAwait(true);
            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(_localization.Format("Share.ImportFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        ImportResult = null;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSectionTitles()
    {
        HasExportTunnelItems = ExportTunnelItems.Count > 0;
        HasExportRdpItems = ExportRdpItems.Count > 0;
        HasImportTunnelItems = ImportTunnelItems.Count > 0;
        HasImportRdpItems = ImportRdpItems.Count > 0;

        ExportTunnelSectionTitle = _localization.Format("Share.SectionTunnels", ExportTunnelItems.Count);
        ExportRdpSectionTitle = _localization.Format("Share.SectionRdp", ExportRdpItems.Count);
        ImportTunnelSectionTitle = _localization.Format("Share.SectionTunnels", ImportTunnelItems.Count);
        ImportRdpSectionTitle = _localization.Format("Share.SectionRdp", ImportRdpItems.Count);
        ImportSummary = HasImportPreview
            ? _localization.Format(
                "Share.ImportSummary",
                ImportTunnelItems.Count(i => i.IsSelected),
                ImportRdpItems.Count(i => i.IsSelected))
            : string.Empty;

        OnPropertyChanged(nameof(HasExportItems));
    }

    private ShareConnectionItemViewModel CreateImportItem(
        ShareConnectionKind kind,
        int bundleIndex,
        string name,
        string subtitle)
    {
        var item = new ShareConnectionItemViewModel
        {
            Kind = kind,
            BundleIndex = bundleIndex,
            Name = name,
            Subtitle = subtitle,
            IsSelected = true
        };
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShareConnectionItemViewModel.IsSelected))
                RefreshSectionTitles();
        };
        return item;
    }

    private static string FormatTunnelSubtitle(string bindAddress, int localPort, string remoteHost, int remotePort)
    {
        var bind = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress.Trim();
        var remote = string.IsNullOrWhiteSpace(remoteHost) ? "?" : remoteHost.Trim();
        return $"{bind}:{localPort} → {remote}:{remotePort}";
    }

    private static string FormatRdpSubtitle(string rdpHost, int rdpPort, string? groupName)
    {
        var host = string.IsNullOrWhiteSpace(rdpHost) ? "?" : rdpHost.Trim();
        var builder = new StringBuilder($"{host}:{rdpPort}");
        if (!string.IsNullOrWhiteSpace(groupName))
            builder.Append(" · ").Append(groupName.Trim());
        return builder.ToString();
    }

    private bool CanExecuteExport() => !IsBusy && IsExportMode;

    private bool CanExecuteBrowse() => !IsBusy && !IsExportMode;
}
