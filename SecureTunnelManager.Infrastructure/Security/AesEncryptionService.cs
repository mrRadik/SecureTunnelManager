using System.Security.Cryptography;
using System.Text;

namespace SecureTunnelManager.Infrastructure.Security;

/// <summary>
/// AES-256 encryption for vault secrets using a master-password-derived key.
/// </summary>
public static class AesEncryptionService
{
    private const int KeySize = 32;
    private const int IvSize = 16;
    private const int SaltSize = 32;
    private const int Iterations = 100_000;

    public static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public static string Encrypt(string plainText, byte[] key)
    {
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherTextBase64, byte[] key)
    {
        var combined = Convert.FromBase64String(cipherTextBase64);
        var iv = combined.AsSpan(0, IvSize).ToArray();
        var cipherBytes = combined.AsSpan(IvSize).ToArray();

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public static string HashPassword(string password, byte[] salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, byte[] salt, string expectedHash)
    {
        var hash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(hash),
            Convert.FromBase64String(expectedHash));
    }
}
