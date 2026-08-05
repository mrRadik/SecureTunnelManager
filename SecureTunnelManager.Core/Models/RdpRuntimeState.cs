namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Live RDP session state exposed to the UI.
/// </summary>
public record RdpRuntimeState
{
    public int TargetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RdpHostDisplay { get; set; } = string.Empty;
    public string LocalEndpoint { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public RdpSessionStatus Status { get; set; } = RdpSessionStatus.Disconnected;
    public string? ErrorMessage { get; set; }
    public DateTime? LastConnectedAt { get; set; }
}
