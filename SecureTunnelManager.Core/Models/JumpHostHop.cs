namespace SecureTunnelManager.Core.Models;

/// <summary>
/// One hop in a jump-host chain (bastion → bastion → …).
/// </summary>
public class JumpHostHop
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Password;
    public int? CredentialId { get; set; }
    public string? PrivateKeyPath { get; set; }
    public int? KeyPassphraseCredentialId { get; set; }
}
