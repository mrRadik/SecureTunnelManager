using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.UI.Services;

public sealed class WhatsNewService
{
    private readonly IUpdateService _updateService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;

    public WhatsNewService(
        IUpdateService updateService,
        ISettingsService settingsService,
        IDialogService dialogService,
        ILocalizationService localization)
    {
        _updateService = updateService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _localization = localization;
    }

    public async Task TryShowWhatsNewAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = _updateService.GetCurrentVersion();
        var versionLabel = AppVersion.ToLabel(currentVersion);
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(settings.LastAcknowledgedVersion))
        {
            await AcknowledgeVersionAsync(versionLabel, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (!AppVersion.TryParseLabel(settings.LastAcknowledgedVersion, out var lastAcknowledged)
            || !AppVersion.IsNewerThan(currentVersion, lastAcknowledged))
        {
            return;
        }

        var releaseNotes = await ResolveReleaseNotesAsync(currentVersion, cancellationToken).ConfigureAwait(true);
        _dialogService.ShowWhatsNew(versionLabel, releaseNotes);
        await AcknowledgeVersionAsync(versionLabel, cancellationToken).ConfigureAwait(true);
    }

    private async Task<string> ResolveReleaseNotesAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        var pending = _updateService.TryConsumePendingReleaseNotes(currentVersion);
        if (pending is not null && !string.IsNullOrWhiteSpace(pending.ReleaseNotes))
            return pending.ReleaseNotes.Trim();

        var manifest = await _updateService
            .GetCurrentVersionManifestAsync(cancellationToken)
            .ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(manifest?.ReleaseNotes))
            return manifest.ReleaseNotes.Trim();

        var bundled = _updateService.TryGetBundledReleaseNotes();
        if (!string.IsNullOrWhiteSpace(bundled))
            return bundled.Trim();

        return _localization.Format("WhatsNew.DefaultNotes", AppVersion.ToLabel(currentVersion));
    }

    private Task AcknowledgeVersionAsync(string versionLabel, CancellationToken cancellationToken) =>
        _settingsService.SetLastAcknowledgedVersionAsync(versionLabel, cancellationToken);
}
