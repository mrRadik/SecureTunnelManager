namespace SecureTunnelManager.Data.Entities;

public class CredentialEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    /// <summary>DPAPI + AES encrypted password blob (Base64).</summary>
    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
