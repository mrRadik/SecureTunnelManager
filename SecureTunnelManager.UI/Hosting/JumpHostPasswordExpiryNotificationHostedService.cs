using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Hosting;

/// <summary>
/// Sends jump host password expiry notifications once per day at 10:00 MSK.
/// </summary>
public sealed class JumpHostPasswordExpiryNotificationHostedService : BackgroundService
{
    private readonly JumpHostPasswordExpiryNotifier _notifier;
    private readonly ILogger<JumpHostPasswordExpiryNotificationHostedService> _logger;

    public JumpHostPasswordExpiryNotificationHostedService(
        JumpHostPasswordExpiryNotifier notifier,
        ILogger<JumpHostPasswordExpiryNotificationHostedService> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafeAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = JumpHostPasswordExpiryNotifier.GetNextNotificationUtc() - DateTime.UtcNow;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            _logger.LogDebug("Next jump host password expiry check in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunSafeAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunSafeAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _notifier.CheckAndNotifyAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Jump host password expiry notification check failed");
        }
    }
}
