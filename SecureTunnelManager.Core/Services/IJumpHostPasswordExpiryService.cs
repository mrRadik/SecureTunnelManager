using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface IJumpHostPasswordExpiryService
{
    public const int WarningDays = 14;

    event EventHandler<JumpHost>? PasswordExpiresAtUpdated;

    bool IsExpiringSoon(JumpHost jumpHost, int days = WarningDays);
}
