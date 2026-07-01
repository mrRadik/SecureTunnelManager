using CommunityToolkit.Mvvm.ComponentModel;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Helpers;

namespace SecureTunnelManager.UI.ViewModels;

public partial class TunnelRowViewModel : ObservableObject
{
    public int ProfileId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string LocalEndpoint { get; set; } = string.Empty;
    public string JumpHostDisplay { get; set; } = string.Empty;
    public IReadOnlyList<string> JumpHostDisplays { get; set; } = Array.Empty<string>();
    public string DestinationDisplay { get; set; } = string.Empty;
    public string TargetDisplay { get; set; } = string.Empty;
    public int LocalPort { get; set; }

    [ObservableProperty]
    private TunnelStatus _status = TunnelStatus.Stopped;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _reconnectAttempt;

    [ObservableProperty]
    private DateTime? _lastConnectedAt;

    public string StatusIcon => Status switch
    {
        TunnelStatus.Connected => "🟢",
        TunnelStatus.Connecting => "🟡",
        TunnelStatus.Error => "🔴",
        _ => "🔴"
    };

    public string StatusText => Status switch
    {
        TunnelStatus.Connected => "Connected",
        TunnelStatus.Connecting => ReconnectAttempt > 0 ? "Reconnecting" : "Connecting",
        TunnelStatus.Error => "Error",
        _ => "Stopped"
    };

    public string LastConnectedDisplay => LastConnectedAt switch
    {
        null when Status == TunnelStatus.Connected => "Just now",
        null => "Never",
        { } dt when Status == TunnelStatus.Connected => dt.ToLocalTime().ToString("g"),
        { } dt => $"Last: {dt.ToLocalTime():g}"
    };

    partial void OnStatusChanged(TunnelStatus value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastConnectedDisplay));
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
    }

    partial void OnReconnectAttemptChanged(int value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIcon));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
    }

    public bool ShowErrorIndicator =>
        Status == TunnelStatus.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

    public string ErrorTooltip => string.IsNullOrWhiteSpace(ErrorMessage)
        ? "An unexpected error occurred."
        : ErrorMessage;

    partial void OnLastConnectedAtChanged(DateTime? value) => OnPropertyChanged(nameof(LastConnectedDisplay));

    public void UpdateFrom(TunnelRuntimeState state)
    {
        Name = state.Name;
        LocalEndpoint = state.LocalEndpoint;
        JumpHostDisplay = state.JumpHostDisplay;
        JumpHostDisplays = state.JumpHostDisplays;
        DestinationDisplay = state.DestinationDisplay;
        TargetDisplay = state.TargetDisplay;
        LocalPort = state.LocalPort;
        Status = state.Status;
        ErrorMessage = state.ErrorMessage;
        ReconnectAttempt = state.ReconnectAttempt;
        LastConnectedAt = state.LastConnectedAt;
        OnPropertyChanged(nameof(ShowErrorIndicator));
        OnPropertyChanged(nameof(ErrorTooltip));
    }

    public static TunnelRowViewModel FromProfile(TunnelProfile profile, TunnelRuntimeState? runtime)
    {
        var row = new TunnelRowViewModel { ProfileId = profile.Id };
        var state = runtime ?? new TunnelRuntimeState { ProfileId = profile.Id, Status = TunnelStatus.Stopped };
        TunnelDisplayHelper.ApplyRoute(state, profile);
        if (runtime is not null)
        {
            state.Status = runtime.Status;
            state.ErrorMessage = runtime.ErrorMessage;
            state.ReconnectAttempt = runtime.ReconnectAttempt;
            state.LastConnectedAt = runtime.LastConnectedAt;
        }
        row.UpdateFrom(state);
        return row;
    }
}
