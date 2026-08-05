namespace SecureTunnelManager.Core.Models;

public static class AppThemeModes
{
    public const string Light = "light";
    public const string Dark = "dark";
    public const string System = "system";

    public static string Normalize(string? mode) =>
        mode?.Trim().ToLowerInvariant() switch
        {
            Light => Light,
            System => System,
            _ => Dark
        };

    public static bool IsLight(string? mode) =>
        string.Equals(Normalize(mode), Light, StringComparison.Ordinal);
}
