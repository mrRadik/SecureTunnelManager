namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Runtime status of an RDP session (SSH forward + mstsc).
/// </summary>
public enum RdpSessionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
