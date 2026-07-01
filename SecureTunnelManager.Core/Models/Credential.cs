namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Stored credential reference (passwords are never held in plain text in memory longer than needed).
/// </summary>
public class Credential
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
