using System.Security.Cryptography;
using System.Text;

namespace SecureTunnelManager.Infrastructure.Security;

/// <summary>
/// Encrypts shared connection files with a build-time embedded key (obfuscation, not user secrets).
/// Key material is injected during build from secrets/share-key.txt or STM_SHARE_ENCRYPTION_KEY.
/// Use one stable secret for all builds; rotate manually when needed.
/// </summary>
internal static partial class ShareFileCrypto
{
    private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes(KeyMaterial));

    public static string EncryptJson(string json) => AesEncryptionService.Encrypt(json, Key);

    public static string DecryptJson(string payload) => AesEncryptionService.Decrypt(payload, Key);
}
