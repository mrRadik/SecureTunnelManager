using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Persistence for RDP computer profiles.
/// </summary>
public interface IRdpTargetService
{
    Task<IReadOnlyList<RdpTarget>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RdpTarget?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(RdpTarget target, CancellationToken cancellationToken = default);
    Task UpdateAsync(RdpTarget target, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetGroupNamesAsync(CancellationToken cancellationToken = default);
    Task SetGroupNameAsync(int targetId, string? groupName, CancellationToken cancellationToken = default);
    Task RenameGroupAsync(string oldName, string newName, CancellationToken cancellationToken = default);
}
