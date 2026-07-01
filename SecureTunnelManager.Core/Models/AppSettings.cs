namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Application settings stored in the database.
/// </summary>
public class AppSettings
{
    public bool VaultInitialized { get; set; }
    public string? MasterPasswordHash { get; set; }
    public string? MasterPasswordSalt { get; set; }
    public bool VaultAutoLockEnabled { get; set; } = true;
    public int VaultAutoLockMinutes { get; set; } = 15;
    public int ReconnectIntervalSeconds { get; set; } = 15;
    public int CircuitBreakerBreakSeconds { get; set; } = 90;
    public bool StartAllTunnelsOnAppStart { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string? LastAcknowledgedVersion { get; set; }
    public string UiLanguage { get; set; } = "en";
}
