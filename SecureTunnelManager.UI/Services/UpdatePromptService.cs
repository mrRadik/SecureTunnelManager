using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.UI.Services;

public sealed class UpdatePromptService
{
    private readonly IUpdateService _updateService;
    private readonly ITunnelManagerService _tunnelManager;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localization;
    private readonly ILogger<UpdatePromptService> _logger;
    private int _installInProgress;

    public UpdatePromptService(
        IUpdateService updateService,
        ITunnelManagerService tunnelManager,
        IDialogService dialogService,
        ILocalizationService localization,
        ILogger<UpdatePromptService> logger)
    {
        _updateService = updateService;
        _tunnelManager = tunnelManager;
        _dialogService = dialogService;
        _localization = localization;
        _logger = logger;
    }

    public bool CanCheckForUpdates => _updateService.IsInstalledViaInstaller();

    public string CurrentVersion => _updateService.GetCurrentVersion().ToString(3);

    public async Task CheckAndPromptAsync(bool silentWhenUpToDate, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _installInProgress, 0, 0) == 1)
            return;

        if (!CanCheckForUpdates)
        {
            if (!silentWhenUpToDate)
                _dialogService.ShowInfo(_localization.Get("Updates.NotInstalled"));

            return;
        }

        UpdateManifest? manifest;
        try
        {
            manifest = await _updateService.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            if (!silentWhenUpToDate)
                _dialogService.ShowError(_localization.Get("Updates.CheckFailed"));

            return;
        }

        if (manifest is null)
        {
            if (!silentWhenUpToDate)
                _dialogService.ShowInfo(_localization.Format("Updates.UpToDate", CurrentVersion));

            return;
        }

        await PromptInstallAsync(manifest, cancellationToken).ConfigureAwait(true);
    }

    private async Task PromptInstallAsync(UpdateManifest manifest, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _installInProgress, 1, 0) != 0)
            return;

        try
        {
            var message = string.IsNullOrWhiteSpace(manifest.ReleaseNotes)
                ? _localization.Format("Updates.Available", manifest.Version, CurrentVersion)
                : _localization.Format("Updates.AvailableWithNotes", manifest.Version, CurrentVersion, manifest.ReleaseNotes);

            if (!_dialogService.ShowConfirm(message, _localization.Get("Updates.Title")))
                return;

            string msiPath;
            try
            {
                msiPath = await _updateService
                    .DownloadUpdateAsync(manifest, progress: null, cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update download failed for version {Version}", manifest.Version);
                _dialogService.ShowError(_localization.Get("Updates.DownloadFailed"));
                return;
            }

            if (!_dialogService.ShowConfirm(
                    _localization.Get("Updates.InstallConfirm"),
                    _localization.Get("Updates.Title")))
            {
                _logger.LogInformation("User cancelled update install for version {Version}", manifest.Version);
                return;
            }

            try
            {
                await _tunnelManager.StopAllAsync(cancellationToken).ConfigureAwait(true);
                _updateService.SavePendingReleaseNotes(manifest.Version, manifest.ReleaseNotes);

                if (!_updateService.LaunchInstaller(msiPath))
                {
                    _dialogService.ShowError(_localization.Get("Updates.InstallFailed"));
                    return;
                }

                // msiexec is elevated and running; CloseApplication in MSI will finish closing us.
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(true);

                if (System.Windows.Application.Current is App app)
                    app.ShutdownApplication();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start installer for version {Version}", manifest.Version);
                _dialogService.ShowError(_localization.Get("Updates.InstallFailed"));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _installInProgress, 0);
        }
    }
}
