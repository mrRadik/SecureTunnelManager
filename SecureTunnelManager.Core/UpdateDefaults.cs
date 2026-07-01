namespace SecureTunnelManager.Core;

public static class UpdateDefaults
{
    /// <summary>
    /// Fixed URL that resolves to the update manifest on the latest GitHub release.
    /// Upload <c>update.json</c> as a release asset with each version.
    /// </summary>
    public const string ManifestUrl =
        "https://github.com/mrRadik/SecureTunnelManager/releases/latest/download/update.json";
}
