using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Low-level SSH tunnel connection management for a single profile.
/// </summary>
public interface ISshTunnelService
{
    Task StartAsync(TunnelProfile profile, CancellationToken cancellationToken = default);
    Task StopAsync(int profileId, CancellationToken cancellationToken = default);
    TunnelStatus GetStatus(int profileId);
    string? GetErrorMessage(int profileId);
}
