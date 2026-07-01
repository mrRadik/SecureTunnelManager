namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Application settings stored in the database.
/// </summary>
public class AppSettings
{
    public bool VaultInitialized { get; set; }
    public string? MasterPasswordHash { get; set; }
    public string? MasterPasswordSalt { get; set; }
    public int VaultAutoLockMinutes { get; set; } = 15;
    public bool MinimizeToTrayOnStart { get; set; } = true;
    public bool StartMinimizedWithWindows { get; set; }
    public bool StartAllTunnelsOnAppStart { get; set; }
    public bool CloseToTray { get; set; } = true;
    public string UiLanguage { get; set; } = "en";
}
