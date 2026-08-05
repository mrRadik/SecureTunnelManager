namespace SecureTunnelManager.Data.Entities;

public class RdpTargetEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string? JumpHostsJson { get; set; }

    public string RdpHost { get; set; } = string.Empty;
    public int RdpPort { get; set; } = 3389;
    public int? RdpCredentialId { get; set; }

    public int LocalPort { get; set; }
    public string LocalBindAddress { get; set; } = "127.0.0.1";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
