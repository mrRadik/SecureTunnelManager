using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// High-level tunnel orchestration with auto-reconnect and status events.
/// </summary>
public interface ITunnelManagerService
{
    event EventHandler<TunnelRuntimeState>? TunnelStateChanged;

    Task StartTunnelAsync(int profileId, CancellationToken cancellationToken = default);
    Task StopTunnelAsync(int profileId, CancellationToken cancellationToken = default);
    Task RestartTunnelAsync(int profileId, CancellationToken cancellationToken = default);
    Task StartAllAsync(CancellationToken cancellationToken = default);
    Task StopAllAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<TunnelRuntimeState> GetRuntimeStates();
    TunnelRuntimeState? GetRuntimeState(int profileId);
}
