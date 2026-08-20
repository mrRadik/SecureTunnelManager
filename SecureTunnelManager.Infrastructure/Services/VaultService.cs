using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Infrastructure.Security;

namespace SecureTunnelManager.Infrastructure.Services;

/// <summary>
/// Master password vault: AES encryption + DPAPI storage layer.
/// </summary>
public class VaultService : IVaultService
{
    private const string VaultInitializedKey = "VaultInitialized";
    private const string MasterPasswordHashKey = "MasterPasswordHash";
    private const string MasterPasswordSaltKey = "MasterPasswordSalt";

    private readonly ISettingsService _settingsService;
    private readonly IVaultUnlockCacheService _unlockCache;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<VaultService> _logger;
    private byte[]? _derivedKey;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private readonly object _lock = new();

    public VaultService(
        ISettingsService settingsService,
        IVaultUnlockCacheService unlockCache,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<VaultService> logger)
    {
        _settingsService = settingsService;
        _unlockCache = unlockCache;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public bool IsInitialized { get; private set; }
    public bool IsUnlocked => _derivedKey is not null;

    public event EventHandler<VaultLockedEventArgs>? VaultLocked;
    public event EventHandler? VaultUnlocked;
    public event EventHandler? VaultReset;

    public async Task<bool> IsVaultInitializedAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        IsInitialized = settings.VaultInitialized;
        return IsInitialized;
    }

    public async Task InitializeVaultAsync(string masterPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterPassword);

        var salt = AesEncryptionService.GenerateSalt();
        var hash = AesEncryptionService.HashPassword(masterPassword, salt);

        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.VaultInitialized = true;
        settings.MasterPasswordHash = hash;
        settings.MasterPasswordSalt = Convert.ToBase64String(salt);
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        UnlockInternal(masterPassword, salt);
        IsInitialized = true;
        _logger.LogInformation("Password vault initialized");
    }

    public async Task<bool> UnlockAsync(string masterPassword, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.VaultInitialized || settings.MasterPasswordSalt is null || settings.MasterPasswordHash is null)
            return false;

        var salt = Convert.FromBase64String(settings.MasterPasswordSalt);
        if (!AesEncryptionService.VerifyPassword(masterPassword, salt, settings.MasterPasswordHash))
            return false;

        UnlockInternal(masterPassword, salt);
        _logger.LogInformation("Password vault unlocked");
        return true;
    }

    public async Task<bool> TryUnlockFromCacheAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.RememberVaultOnThisDevice
            || !settings.VaultInitialized
            || settings.MasterPasswordHash is null)
        {
            return false;
        }

        var derivedKey = await _unlockCache.TryLoadAsync(settings.MasterPasswordHash, cancellationToken).ConfigureAwait(false);
        if (derivedKey is null)
        {
            await DisableRememberUnlockAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        UnlockInternalFromKey(derivedKey);
        _logger.LogInformation("Password vault unlocked from device cache");
        return true;
    }

    public async Task<bool> HasCachedUnlockKeyAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.RememberVaultOnThisDevice
            || !settings.VaultInitialized
            || settings.MasterPasswordHash is null)
        {
            return false;
        }

        return _unlockCache.HasMatchingCache(settings.MasterPasswordHash);
    }

    public async Task ApplyRememberUnlockAsync(bool remember, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.RememberVaultOnThisDevice = remember;
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        if (!remember)
        {
            _unlockCache.Clear();
            return;
        }

        if (_derivedKey is null || settings.MasterPasswordHash is null)
            return;

        byte[] keyCopy;
        lock (_lock)
        {
            keyCopy = _derivedKey.ToArray();
        }

        try
        {
            await _unlockCache.SaveAsync(settings.MasterPasswordHash, keyCopy, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Vault unlock key saved for this device");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyCopy);
        }
    }

    public async Task ClearRememberUnlockAsync(CancellationToken cancellationToken = default)
    {
        _unlockCache.Clear();
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.RememberVaultOnThisDevice)
            return;

        settings.RememberVaultOnThisDevice = false;
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public void Lock(bool manual = false)
    {
        lock (_lock)
        {
            if (_derivedKey is not null)
            {
                CryptographicOperations.ZeroMemory(_derivedKey);
                _derivedKey = null;
            }
        }

        _logger.LogInformation("Password vault locked");
        VaultLocked?.Invoke(this, new VaultLockedEventArgs(manual));
    }

    public void NotifyActivity() => _lastActivityUtc = DateTime.UtcNow;

    public DateTime LastActivityUtc => _lastActivityUtc;

    public async Task<string> EncryptSecretAsync(string plainText, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        NotifyActivity();

        var aesEncrypted = AesEncryptionService.Encrypt(plainText, _derivedKey!);
        var dpapiProtected = DpapiProtectionService.Protect(aesEncrypted);
        return await Task.FromResult(dpapiProtected).ConfigureAwait(false);
    }

    public async Task<string> DecryptSecretAsync(string encryptedText, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        NotifyActivity();

        var aesEncrypted = DpapiProtectionService.Unprotect(encryptedText);
        var plain = AesEncryptionService.Decrypt(aesEncrypted, _derivedKey!);
        return await Task.FromResult(plain).ConfigureAwait(false);
    }

    public async Task<bool> VerifyMasterPasswordAsync(string masterPassword, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (settings.MasterPasswordSalt is null || settings.MasterPasswordHash is null)
            return false;

        var salt = Convert.FromBase64String(settings.MasterPasswordSalt);
        return AesEncryptionService.VerifyPassword(masterPassword, salt, settings.MasterPasswordHash);
    }

    public async Task ChangeMasterPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!await UnlockAsync(currentPassword, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Current master password is incorrect.");

        var newSalt = AesEncryptionService.GenerateSalt();
        var newHash = AesEncryptionService.HashPassword(newPassword, newSalt);

        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.MasterPasswordHash = newHash;
        settings.MasterPasswordSalt = Convert.ToBase64String(newSalt);
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        UnlockInternal(newPassword, newSalt);
        _logger.LogInformation("Master password changed");

        if (settings.RememberVaultOnThisDevice)
            await ApplyRememberUnlockAsync(true, cancellationToken).ConfigureAwait(false);
        else
            _unlockCache.Clear();
    }

    public async Task ResetVaultAsync(string newMasterPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newMasterPassword);

        Lock();
        await ClearRememberUnlockAsync(cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Credentials.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.TunnelProfiles.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.RdpTargets.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var vaultKeys = new[] { VaultInitializedKey, MasterPasswordHashKey, MasterPasswordSaltKey };
        await db.Settings.Where(s => vaultKeys.Contains(s.Key))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        IsInitialized = false;
        VaultReset?.Invoke(this, EventArgs.Empty);

        await InitializeVaultAsync(newMasterPassword, cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("Vault reset completed: all tunnels and credentials were deleted");
    }

    private void UnlockInternal(string masterPassword, byte[] salt)
    {
        var derivedKey = AesEncryptionService.DeriveKey(masterPassword, salt);
        UnlockInternalFromKey(derivedKey);
    }

    private void UnlockInternalFromKey(byte[] derivedKey)
    {
        lock (_lock)
        {
            if (_derivedKey is not null)
                CryptographicOperations.ZeroMemory(_derivedKey);

            _derivedKey = derivedKey;
            _lastActivityUtc = DateTime.UtcNow;
        }

        VaultUnlocked?.Invoke(this, EventArgs.Empty);
    }

    private async Task DisableRememberUnlockAsync(CancellationToken cancellationToken = default)
    {
        _unlockCache.Clear();

        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.RememberVaultOnThisDevice)
            return;

        settings.RememberVaultOnThisDevice = false;
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureUnlocked()
    {
        if (_derivedKey is null)
            throw new InvalidOperationException("Vault is locked. Unlock with master password first.");
    }
}
