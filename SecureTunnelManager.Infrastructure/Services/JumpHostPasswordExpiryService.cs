using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Ssh;

namespace SecureTunnelManager.Infrastructure.Services;

public sealed class JumpHostPasswordExpiryService : IJumpHostPasswordExpiryService, IJumpHostPasswordExpiryProbe
{
    private readonly IJumpHostService _jumpHostService;
    private readonly ILogger<JumpHostPasswordExpiryService> _logger;

    public JumpHostPasswordExpiryService(
        IJumpHostService jumpHostService,
        ILogger<JumpHostPasswordExpiryService> logger)
    {
        _jumpHostService = jumpHostService;
        _logger = logger;
    }

    public event EventHandler<JumpHost>? PasswordExpiresAtUpdated;

    public async Task ProbeIfNeededAsync(SshClient client, JumpHostHop hop, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
            return;

        if (hop.JumpHostEntityId is not int jumpHostId || jumpHostId <= 0)
            return;

        if (hop.AuthMethod != AuthMethod.Password)
            return;

        if (string.IsNullOrWhiteSpace(hop.Username))
            return;

        var jumpHost = await _jumpHostService.GetByIdAsync(jumpHostId, cancellationToken).ConfigureAwait(false);
        if (jumpHost is null || jumpHost.HasKnownPasswordExpiry)
            return;

        try
        {
            var probe = await Task.Run(
                () => WindowsPasswordExpiryProbe.Run(client, hop.Username, _logger),
                cancellationToken).ConfigureAwait(false);

            if (!probe.Succeeded)
            {
                _logger.LogDebug("Password expiry probe for jump host {JumpHostId} did not return a date", jumpHostId);
                return;
            }

            var expiresAt = probe.NeverExpires ? JumpHostPasswordExpiry.NeverExpires : probe.ExpiresAt;
            var updated = await _jumpHostService.SetPasswordExpiresAtIfEmptyAsync(jumpHostId, expiresAt, cancellationToken)
                .ConfigureAwait(false);
            if (updated is null)
                return;

            _logger.LogInformation(
                "Password expiry for jump host {Name} set to {ExpiresAt}",
                updated.Name,
                expiresAt == JumpHostPasswordExpiry.NeverExpires ? "never" : expiresAt?.ToString("u"));

            PasswordExpiresAtUpdated?.Invoke(this, updated);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Password expiry probe failed for jump host {JumpHostId}", jumpHostId);
        }
    }

    public bool IsExpiringSoon(JumpHost jumpHost, int days = IJumpHostPasswordExpiryService.WarningDays)
    {
        if (jumpHost.PasswordExpiresAt is not DateTime expiresAt || expiresAt == JumpHostPasswordExpiry.NeverExpires)
            return false;

        return (expiresAt.Date - DateTime.Today).TotalDays <= days;
    }
}
