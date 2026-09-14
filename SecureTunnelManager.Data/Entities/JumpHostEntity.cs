namespace SecureTunnelManager.Data.Entities;

public class JumpHostEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public int AuthMethod { get; set; }
    public int? CredentialId { get; set; }
    public string? PrivateKeyPath { get; set; }
    public int? KeyPassphraseCredentialId { get; set; }
    public DateTime? PasswordExpiresAt { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
