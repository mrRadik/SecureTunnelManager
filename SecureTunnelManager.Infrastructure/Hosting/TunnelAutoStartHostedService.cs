using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Hosting;

/// <summary>
/// Starts tunnels marked with StartWithWindows on application launch.
/// </summary>
public class TunnelAutoStartHostedService : BackgroundService
{
    private readonly ITunnelProfileService _profileService;
    private readonly ITunnelManagerService _tunnelManager;
    private readonly ILogger<TunnelAutoStartHostedService> _logger;

    public TunnelAutoStartHostedService(
        ITunnelProfileService profileService,
        ITunnelManagerService tunnelManager,
        ILogger<TunnelAutoStartHostedService> logger)
    {
        _profileService = profileService;
        _tunnelManager = tunnelManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow UI and vault unlock to initialize first
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        try
        {
            var profiles = await _profileService.GetAutoStartProfilesAsync(stoppingToken).ConfigureAwait(false);
            if (profiles.Count == 0)
                return;

            _logger.LogInformation("Auto-starting {Count} tunnel(s)", profiles.Count);

            foreach (var profile in profiles)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await _tunnelManager.StartTunnelAsync(profile.Id, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-start tunnel {Name}", profile.Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
    }
}
