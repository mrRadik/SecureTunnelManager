using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task SetLastAcknowledgedVersionAsync(string versionLabel, CancellationToken cancellationToken = default);
}
