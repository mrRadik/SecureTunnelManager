using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// CRUD operations for stored credentials (secrets encrypted via vault).
/// </summary>
public interface ICredentialService
{
    Task<IReadOnlyList<Credential>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Credential?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(string name, string username, string password, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, string name, string username, string? password, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordAsync(int id, string password, CancellationToken cancellationToken = default);
    Task<string?> GetPasswordAsync(int credentialId, CancellationToken cancellationToken = default);
}
