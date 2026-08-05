using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Orchestrates SSH forward + mstsc for RDP targets.
/// </summary>
public interface IRdpSessionService
{
    event EventHandler<RdpRuntimeState>? SessionStateChanged;

    Task ConnectAsync(int targetId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(int targetId, CancellationToken cancellationToken = default);
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<RdpRuntimeState> GetRuntimeStates();
    RdpRuntimeState? GetRuntimeState(int targetId);
}
