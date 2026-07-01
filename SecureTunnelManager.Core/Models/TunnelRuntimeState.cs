namespace SecureTunnelManager.Core.Models;



/// <summary>

/// Live tunnel state exposed to the UI layer.

/// </summary>

public record TunnelRuntimeState

{

    public int ProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string LocalEndpoint { get; set; } = string.Empty;

    public string JumpHostDisplay { get; set; } = string.Empty;

    public List<string> JumpHostDisplays { get; set; } = new();

    public string DestinationDisplay { get; set; } = string.Empty;

    public string TargetDisplay { get; set; } = string.Empty;

    public int LocalPort { get; set; }

    public TunnelStatus Status { get; set; } = TunnelStatus.Stopped;

    public string? ErrorMessage { get; set; }

    public int ReconnectAttempt { get; set; }

    public DateTime? LastConnectedAt { get; set; }

}

