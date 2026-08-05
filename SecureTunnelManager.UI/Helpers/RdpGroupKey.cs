namespace SecureTunnelManager.UI.Helpers;

internal static class RdpGroupKey
{
    public const string Ungrouped = "";

    public static string Normalize(string? groupName) =>
        string.IsNullOrWhiteSpace(groupName) ? Ungrouped : groupName.Trim();

    public static bool IsUngrouped(string key) => key == Ungrouped;
}
