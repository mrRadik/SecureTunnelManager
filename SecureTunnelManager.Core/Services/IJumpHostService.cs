using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface IJumpHostService
{
    Task<IReadOnlyList<JumpHost>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<JumpHost?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<JumpHost?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(JumpHost jumpHost, CancellationToken cancellationToken = default);
    Task UpdateAsync(JumpHost jumpHost, CancellationToken cancellationToken = default);
    /// <summary>Sets password expiry only when the field is currently empty.</summary>
    Task<JumpHost?> SetPasswordExpiresAtIfEmptyAsync(int id, DateTime? expiresAt, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JumpHostHop>> ResolveHopsAsync(
        IReadOnlyList<JumpHostHop> hops,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetReferenceCountsAsync(CancellationToken cancellationToken = default);
}
