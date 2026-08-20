namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Stores a DPAPI-protected vault derived key for unlock on this Windows user account.
/// </summary>
public interface IVaultUnlockCacheService
{
    bool Exists { get; }

    Task SaveAsync(string passwordHash, byte[] derivedKey, CancellationToken cancellationToken = default);

    Task<byte[]?> TryLoadAsync(string passwordHash, CancellationToken cancellationToken = default);

    bool HasMatchingCache(string passwordHash);

    void Clear();
}
