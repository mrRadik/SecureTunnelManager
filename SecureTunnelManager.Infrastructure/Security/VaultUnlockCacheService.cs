using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Security;

public sealed class VaultUnlockCacheService : IVaultUnlockCacheService
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("SecureTunnelManager.VaultUnlock.v1");

    private readonly string _cacheFilePath;

    public VaultUnlockCacheService(string cacheFilePath) => _cacheFilePath = cacheFilePath;

    public bool Exists => File.Exists(_cacheFilePath);

    public Task SaveAsync(string passwordHash, byte[] derivedKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(derivedKey);

        var directory = Path.GetDirectoryName(_cacheFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var payload = new CachePayload
        {
            PasswordHash = passwordHash,
            ProtectedKey = ProtectKey(derivedKey)
        };

        var json = JsonSerializer.Serialize(payload);
        File.WriteAllText(_cacheFilePath, json);
        return Task.CompletedTask;
    }

    public Task<byte[]?> TryLoadAsync(string passwordHash, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cacheFilePath))
            return Task.FromResult<byte[]?>(null);

        try
        {
            var payload = ReadPayload();
            if (payload is null
                || !string.Equals(payload.PasswordHash, passwordHash, StringComparison.Ordinal))
            {
                return Task.FromResult<byte[]?>(null);
            }

            return Task.FromResult<byte[]?>(UnprotectKey(payload.ProtectedKey));
        }
        catch
        {
            Clear();
            return Task.FromResult<byte[]?>(null);
        }
    }

    public bool HasMatchingCache(string passwordHash)
    {
        if (!File.Exists(_cacheFilePath))
            return false;

        try
        {
            var payload = ReadPayload();
            return payload is not null
                   && string.Equals(payload.PasswordHash, passwordHash, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private CachePayload? ReadPayload()
    {
        var json = File.ReadAllText(_cacheFilePath);
        return JsonSerializer.Deserialize<CachePayload>(json);
    }

    public void Clear()
    {
        if (File.Exists(_cacheFilePath))
            File.Delete(_cacheFilePath);
    }

    private static string ProtectKey(byte[] key)
    {
        var protectedBytes = ProtectedData.Protect(key, OptionalEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static byte[] UnprotectKey(string protectedBase64)
    {
        var protectedBytes = Convert.FromBase64String(protectedBase64);
        return ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
    }

    private sealed class CachePayload
    {
        public string PasswordHash { get; set; } = string.Empty;

        public string ProtectedKey { get; set; } = string.Empty;
    }
}
