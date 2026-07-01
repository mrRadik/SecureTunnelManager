using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<VaultService> _logger;
    private byte[]? _derivedKey;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private readonly object _lock = new();

    public VaultService(
        ISettingsService settingsService,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<VaultService> logger)
    {
        _settingsService = settingsService;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public bool IsInitialized { get; private set; }
    public bool IsUnlocked => _derivedKey is not null;

    public event EventHandler? VaultLocked;
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

    public void Lock()
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
        VaultLocked?.Invoke(this, EventArgs.Empty);
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

        // Re-encryption of all credentials must be done by CredentialService caller after password change.
        var newSalt = AesEncryptionService.GenerateSalt();
        var newHash = AesEncryptionService.HashPassword(newPassword, newSalt);

        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.MasterPasswordHash = newHash;
        settings.MasterPasswordSalt = Convert.ToBase64String(newSalt);
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        UnlockInternal(newPassword, newSalt);
        _logger.LogInformation("Master password changed");
    }

    public async Task ResetVaultAsync(string newMasterPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newMasterPassword);

        Lock();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Credentials.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.TunnelProfiles.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

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
        lock (_lock)
        {
            _derivedKey = AesEncryptionService.DeriveKey(masterPassword, salt);
            _lastActivityUtc = DateTime.UtcNow;
        }

        VaultUnlocked?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureUnlocked()
    {
        if (_derivedKey is null)
            throw new InvalidOperationException("Vault is locked. Unlock with master password first.");
    }
}
