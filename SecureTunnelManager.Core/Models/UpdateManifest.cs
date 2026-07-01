namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Update manifest published alongside MSI releases.
/// </summary>
public class UpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }

    public void Normalize()
    {
        Version = Version.Trim();
        Url = Url.Trim();
        Sha256 = Sha256.Trim();
        ReleaseNotes = ReleaseNotes?.Trim();
    }
}
