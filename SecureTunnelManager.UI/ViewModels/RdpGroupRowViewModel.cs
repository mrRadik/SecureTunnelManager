using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class RdpGroupRowViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public RdpGroupRowViewModel(ILocalizationService localization) => _localization = localization;

    public string GroupKey { get; init; } = string.Empty;

    public string DisplayName => RdpGroupKey.IsUngrouped(GroupKey)
        ? _localization.Get("Rdp.Group.Ungrouped")
        : GroupKey;

    public ObservableCollection<RdpTargetRowViewModel> Computers { get; } = new();

    [ObservableProperty]
    private bool _isExpanded = true;

    public int ComputerCount => Computers.Count;

    public int ConnectedCount => Computers.Count(c => c.Status == RdpSessionStatus.Connected);

    public string HeaderText => _localization.Format("Rdp.Group.Header", DisplayName, ComputerCount);

    public string ConnectedSummary => _localization.Format("Rdp.Group.Connected", ConnectedCount);

    public bool HasConnected => ConnectedCount > 0;

    public void RefreshHeader()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ComputerCount));
        OnPropertyChanged(nameof(ConnectedCount));
        OnPropertyChanged(nameof(ConnectedSummary));
        OnPropertyChanged(nameof(HasConnected));
        OnPropertyChanged(nameof(HeaderText));
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(IsExpanded));
}
