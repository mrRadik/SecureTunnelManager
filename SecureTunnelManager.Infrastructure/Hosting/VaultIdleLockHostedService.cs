using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Services;

namespace SecureTunnelManager.Infrastructure.Hosting;

/// <summary>
/// Locks the password vault after configured idle timeout.
/// </summary>
public class VaultIdleLockHostedService : BackgroundService
{
    private readonly VaultService _vaultService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<VaultIdleLockHostedService> _logger;

    public VaultIdleLockHostedService(
        IVaultService vaultService,
        ISettingsService settingsService,
        ILogger<VaultIdleLockHostedService> logger)
    {
        _vaultService = (VaultService)vaultService;
        _settingsService = settingsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);

            if (!_vaultService.IsUnlocked)
                continue;

            var settings = await _settingsService.GetSettingsAsync(stoppingToken).ConfigureAwait(false);
            var idle = DateTime.UtcNow - _vaultService.LastActivityUtc;

            if (idle >= TimeSpan.FromMinutes(settings.VaultAutoLockMinutes))
            {
                _logger.LogInformation("Vault auto-locked after {Minutes} minutes of inactivity", settings.VaultAutoLockMinutes);
                _vaultService.Lock();
            }
        }
    }
}
