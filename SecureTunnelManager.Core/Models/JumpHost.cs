namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Reusable jump host profile stored in the library.
/// </summary>
public class JumpHost
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;
    public int? CredentialId { get; set; }
    public string? PrivateKeyPath { get; set; }
    public int? KeyPassphraseCredentialId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name)
            ? (Port == 22 ? $"{Username}@{Host}" : $"{Username}@{Host}:{Port}")
            : Name;

    public JumpHostHop ToHop() => new()
    {
        JumpHostEntityId = Id,
        Host = Host,
        Port = Port,
        Username = Username,
        AuthMethod = AuthMethod,
        CredentialId = CredentialId,
        PrivateKeyPath = PrivateKeyPath,
        KeyPassphraseCredentialId = KeyPassphraseCredentialId
    };
}
