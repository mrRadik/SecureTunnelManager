namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Password vault with master password protection and DPAPI encryption layer.
/// </summary>
public interface IVaultService
{
    bool IsInitialized { get; }
    bool IsUnlocked { get; }

    event EventHandler? VaultLocked;
    event EventHandler? VaultUnlocked;

    Task<bool> IsVaultInitializedAsync(CancellationToken cancellationToken = default);
    Task InitializeVaultAsync(string masterPassword, CancellationToken cancellationToken = default);
    Task<bool> UnlockAsync(string masterPassword, CancellationToken cancellationToken = default);
    void Lock();
    void NotifyActivity();

    Task<string> EncryptSecretAsync(string plainText, CancellationToken cancellationToken = default);
    Task<string> DecryptSecretAsync(string encryptedText, CancellationToken cancellationToken = default);
    Task<bool> VerifyMasterPasswordAsync(string masterPassword, CancellationToken cancellationToken = default);
    Task ChangeMasterPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all tunnels and credentials, then creates a new vault with the given password.
    /// </summary>
    Task ResetVaultAsync(string newMasterPassword, CancellationToken cancellationToken = default);

    event EventHandler? VaultReset;
}
