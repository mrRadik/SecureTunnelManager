namespace SecureTunnelManager.Data.Entities;

public class TunnelProfileEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string JumpHost { get; set; } = string.Empty;
    public int JumpPort { get; set; } = 22;
    public string JumpUsername { get; set; } = string.Empty;
    public int JumpAuthMethod { get; set; }
    public int? JumpCredentialId { get; set; }
    public string? JumpPrivateKeyPath { get; set; }
    public int? JumpKeyPassphraseCredentialId { get; set; }

    public string? JumpHostsJson { get; set; }

    public string TargetHost { get; set; } = string.Empty;
    public int TargetPort { get; set; } = 22;
    public string TargetUsername { get; set; } = string.Empty;
    public int TargetAuthMethod { get; set; }
    public int? TargetCredentialId { get; set; }
    public string? TargetPrivateKeyPath { get; set; }
    public int? TargetKeyPassphraseCredentialId { get; set; }

    public int LocalPort { get; set; }
    public string LocalBindAddress { get; set; } = "127.0.0.1";
    public string RemoteHost { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; }

    public bool StartWithWindows { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
