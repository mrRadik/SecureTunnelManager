using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class TunnelGroupRowViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public TunnelGroupRowViewModel(ILocalizationService localization) => _localization = localization;

    public string GroupKey { get; init; } = string.Empty;

    public string DisplayName => RdpGroupKey.IsUngrouped(GroupKey)
        ? _localization.Get("Tunnels.Group.Ungrouped")
        : GroupKey;

    public ObservableCollection<TunnelRowViewModel> Tunnels { get; } = new();

    [ObservableProperty]
    private bool _isExpanded = true;

    public int TunnelCount => Tunnels.Count;

    public int ConnectedCount => Tunnels.Count(t => t.Status == TunnelStatus.Connected);

    public string HeaderText => _localization.Format("Tunnels.Group.Header", DisplayName, TunnelCount);

    public string ConnectedSummary => _localization.Format("Tunnels.Group.Connected", ConnectedCount);

    public bool HasConnected => ConnectedCount > 0;

    public void RefreshHeader()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TunnelCount));
        OnPropertyChanged(nameof(ConnectedCount));
        OnPropertyChanged(nameof(ConnectedSummary));
        OnPropertyChanged(nameof(HasConnected));
        OnPropertyChanged(nameof(HeaderText));
    }
}
