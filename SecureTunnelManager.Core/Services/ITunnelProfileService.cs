using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Persistence for tunnel profiles.
/// </summary>
public interface ITunnelProfileService
{
    Task<IReadOnlyList<TunnelProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TunnelProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(TunnelProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(TunnelProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TunnelProfile>> GetAutoStartProfilesAsync(CancellationToken cancellationToken = default);
}
