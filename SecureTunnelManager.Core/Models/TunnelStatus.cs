namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Runtime status of an SSH tunnel connection.
/// </summary>
public enum TunnelStatus
{
    Stopped,
    Connecting,
    Connected,
    Error
}
