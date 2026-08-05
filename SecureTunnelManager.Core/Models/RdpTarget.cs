namespace SecureTunnelManager.Core.Models;

/// <summary>
/// RDP computer profile: bastion hop chain + remote desktop endpoint.
/// </summary>
public class RdpTarget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;

    /// <summary>Optional group label for organizing computers on the RDP page.</summary>
    public string? GroupName { get; set; }

    public List<JumpHostHop> JumpHosts { get; set; } = new();

    public string RdpHost { get; set; } = string.Empty;
    public int RdpPort { get; set; } = 3389;

    /// <summary>Optional vault credential used for mstsc auto-login.</summary>
    public int? RdpCredentialId { get; set; }

    /// <summary>0 = allocate a free local port for each session.</summary>
    public int LocalPort { get; set; }

    public string LocalBindAddress { get; set; } = "127.0.0.1";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
}
