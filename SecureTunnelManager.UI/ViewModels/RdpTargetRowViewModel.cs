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
    public string RdpHostDisplay { get; set; } = string.Empty;
    public string LocalEndpoint { get; set; } = string.Empty;
    public int LocalPort { get; set; }

    [ObservableProperty]
    private RdpSessionStatus _status = RdpSessionStatus.Disconnected;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DateTime? _lastConnectedAt;

    public string StatusText => Status switch
    {
        RdpSessionStatus.Connected => _localization.Get("Rdp.Status.Connected"),
        RdpSessionStatus.Connecting => _localization.Get("Rdp.Status.Connecting"),
        RdpSessionStatus.Error => _localization.Get("Rdp.Status.Error"),
        _ => _localization.Get("Rdp.Status.Disconnected")
    };

    public bool CanConnect => Status is RdpSessionStatus.Disconnected or RdpSessionStatus.Error;
    public bool CanDisconnect => Status is RdpSessionStatus.Connected or RdpSessionStatus.Connecting;

    public bool ShowErrorIndicator =>
        Status == RdpSessionStatus.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

    public string ErrorTooltip => string.IsNullOrWhiteSpace(ErrorMessage)
        ? _localization.Get("Rdp.UnexpectedError")
        : ErrorMessage;

    public void ApplyRuntime(RdpRuntimeState state)
    {
        Status = state.Status;
        ErrorMessage = state.ErrorMessage;
        LocalEndpoint = state.LocalEndpoint;
        LocalPort = state.LocalPort;
        LastConnectedAt = state.LastConnectedAt;
        if (!string.IsNullOrWhiteSpace(state.Name))
            Name = state.Name;
        if (!string.IsNullOrWhiteSpace(state.RdpHostDisplay))
            RdpHostDisplay = state.RdpHostDisplay;

        OnPropertyChanged(nameof(LocalEndpoint));
        OnPropertyChanged(nameof(LocalPort));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(RdpHostDisplay));
    }

    public void RefreshLocalized()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ErrorTooltip));
    }

    partial void OnStatusChanged(RdpSessionStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanDisconnect));
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
    }
}
