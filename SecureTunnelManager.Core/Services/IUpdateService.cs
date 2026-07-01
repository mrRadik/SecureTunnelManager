using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface IUpdateService
{
    Version GetCurrentVersion();

    bool IsInstalledViaInstaller();

    Task<UpdateManifest?> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadUpdateAsync(
        UpdateManifest manifest,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    bool LaunchInstaller(string msiPath);

    void SavePendingReleaseNotes(string version, string? releaseNotes);

    UpdateManifest? TryConsumePendingReleaseNotes(Version expectedVersion);

    Task<UpdateManifest?> GetCurrentVersionManifestAsync(CancellationToken cancellationToken = default);

    string? TryGetBundledReleaseNotes();
}
