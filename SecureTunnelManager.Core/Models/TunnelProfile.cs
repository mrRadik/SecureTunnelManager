namespace SecureTunnelManager.Core.Models;

/// <summary>
/// SSH tunnel profile with jump host and target server configuration.
/// </summary>
public partial class TunnelProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Jump host
    public string JumpHost { get; set; } = string.Empty;
    public int JumpPort { get; set; } = 22;
    public string JumpUsername { get; set; } = string.Empty;
    public AuthMethod JumpAuthMethod { get; set; } = AuthMethod.Password;
    public int? JumpCredentialId { get; set; }
    public string? JumpPrivateKeyPath { get; set; }
    public int? JumpKeyPassphraseCredentialId { get; set; }

    // Target server
    public string TargetHost { get; set; } = string.Empty;
    public int TargetPort { get; set; } = 22;
    public string TargetUsername { get; set; } = string.Empty;
    public AuthMethod TargetAuthMethod { get; set; } = AuthMethod.Password;
    public int? TargetCredentialId { get; set; }
    public string? TargetPrivateKeyPath { get; set; }
    public int? TargetKeyPassphraseCredentialId { get; set; }

    // Port forward
    public string LocalBindAddress { get; set; } = "127.0.0.1";
    public int LocalPort { get; set; }
    public string RemoteHost { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; }

    public bool StartWithWindows { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
