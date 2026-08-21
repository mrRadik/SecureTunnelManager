using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Hosting;

/// <summary>
/// Checks for software updates every hour while the app is running.
/// </summary>
public sealed class UpdateCheckHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly UpdatePromptService _updatePromptService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<UpdateCheckHostedService> _logger;

    public UpdateCheckHostedService(
        UpdatePromptService updatePromptService,
        ISettingsService settingsService,
        ILogger<UpdateCheckHostedService> logger)
    {
        _updatePromptService = updatePromptService;
        _settingsService = settingsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync(stoppingToken).ConfigureAwait(false);
                if (settings.CheckForUpdatesOnStartup)
                {
                    _logger.LogDebug("Running periodic update check");
                    await _updatePromptService
                        .CheckAndPromptAsync(silentWhenUpToDate: true, stoppingToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Periodic update check failed");
            }

            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
