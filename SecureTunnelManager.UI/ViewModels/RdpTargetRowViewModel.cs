using CommunityToolkit.Mvvm.ComponentModel;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public partial class RdpTargetRowViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    public RdpTargetRowViewModel(ILocalizationService localization) => _localization = localization;

    public int TargetId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public string RdpHostDisplay { get; set; } = string.Empty;
    public string RdpHost { get; set; } = string.Empty;
    public int RdpPort { get; set; }
    public string LocalEndpoint { get; set; } = string.Empty;
    public int LocalPort { get; set; }

    public string PortDisplay => RdpPort.ToString();

    public string GroupDisplay => string.IsNullOrWhiteSpace(GroupName)
        ? _localization.Get("Rdp.Group.Ungrouped")
        : GroupName;

    [ObservableProperty]
    private RdpSessionStatus _status = RdpSessionStatus.Disconnected;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DateTime? _lastConnectedAt;

    public string StatusText => Status switch
    {
        RdpSessionStatus.Connected => _localization.Get("Status.Connected"),
        RdpSessionStatus.Connecting => _localization.Get("Status.Connecting"),
        RdpSessionStatus.Error => _localization.Get("Status.Error"),
        _ => _localization.Get("Status.Disconnected")
    };

    public bool CanConnect => Status is RdpSessionStatus.Disconnected or RdpSessionStatus.Error;
    public bool CanDisconnect => Status is RdpSessionStatus.Connected or RdpSessionStatus.Connecting;

    public bool ShowErrorIndicator =>
        Status == RdpSessionStatus.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

    public string ErrorTooltip => string.IsNullOrWhiteSpace(ErrorMessage)
        ? _localization.Get("Rdp.UnexpectedError")
        : ErrorMessage;

    public string TileTooltip
    {
        get
        {
            var lines = new List<string> { Name };

            if (!string.IsNullOrWhiteSpace(Description))
                lines.Add(Description.Trim());

            lines.Add(RdpHostDisplay);
            lines.Add(StatusText);

            if (Status == RdpSessionStatus.Connected && !string.IsNullOrWhiteSpace(LocalEndpoint))
                lines.Add(LocalEndpoint);

            if (!string.IsNullOrWhiteSpace(ErrorMessage))
                lines.Add(ErrorMessage);

            return string.Join(Environment.NewLine, lines);
        }
    }

    public void ApplyRuntime(RdpRuntimeState state)
    {
        Status = state.Status;
        ErrorMessage = state.ErrorMessage;
        LocalEndpoint = state.LocalEndpoint;
        LocalPort = state.LocalPort;
        LastConnectedAt = state.LastConnectedAt;

        OnPropertyChanged(nameof(LocalEndpoint));
        OnPropertyChanged(nameof(LocalPort));
        RefreshTileTooltip();
    }

    public void RefreshLocalized()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ErrorTooltip));
        RefreshTileTooltip();
    }

    private void RefreshTileTooltip() => OnPropertyChanged(nameof(TileTooltip));

    partial void OnStatusChanged(RdpSessionStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanDisconnect));
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
        RefreshTileTooltip();
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
        RefreshTileTooltip();
    }
}
