using System.Globalization;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.UI.Services;

public sealed class JumpHostPasswordExpiryNotifier
{
    internal static readonly TimeSpan NotificationTimeMsk = TimeSpan.FromHours(10);

    private readonly IJumpHostService _jumpHostService;
    private readonly IJumpHostPasswordExpiryService _passwordExpiryService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notifications;
    private readonly ILogger<JumpHostPasswordExpiryNotifier> _logger;

    public JumpHostPasswordExpiryNotifier(
        IJumpHostService jumpHostService,
        IJumpHostPasswordExpiryService passwordExpiryService,
        ISettingsService settingsService,
        INotificationService notifications,
        ILogger<JumpHostPasswordExpiryNotifier> logger)
    {
        _jumpHostService = jumpHostService;
        _passwordExpiryService = passwordExpiryService;
        _settingsService = settingsService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task CheckAndNotifyAsync(CancellationToken cancellationToken = default)
    {
        var mskNow = GetMoscowNow();
        var today = mskNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (mskNow.TimeOfDay < NotificationTimeMsk)
            return;

        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(settings.JumpHostPasswordExpiryLastNotifiedDate, today, StringComparison.Ordinal))
            return;

        var jumpHosts = await _jumpHostService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var notified = 0;

        foreach (var jumpHost in jumpHosts)
        {
            if (!_passwordExpiryService.IsExpiringSoon(jumpHost))
                continue;

            var expiresText = FormatExpiryDate(jumpHost.PasswordExpiresAt!.Value);
            _notifications.Publish(new AppNotification
            {
                Severity = NotificationSeverity.Warning,
                MessageKey = "Notification.JumpHostPasswordExpiring",
                MessageArgs = [jumpHost.Name, expiresText],
                ActionKind = NotificationActionKind.EditJumpHost,
                ResourceId = jumpHost.Id,
                ActionLabelKey = "Notification.EditJumpHost"
            });
            notified++;
        }

        settings.JumpHostPasswordExpiryLastNotifiedDate = today;
        await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Jump host password expiry daily check ({Date} MSK): {Count} notification(s) sent",
            today,
            notified);
    }

    internal static DateTime GetMoscowNow() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MoscowTimeZone.Get());

    internal static DateTime GetNextNotificationUtc()
    {
        var mskNow = GetMoscowNow();
        var nextMsk = mskNow.TimeOfDay < NotificationTimeMsk
            ? mskNow.Date.Add(NotificationTimeMsk)
            : mskNow.Date.AddDays(1).Add(NotificationTimeMsk);
        return TimeZoneInfo.ConvertTimeToUtc(nextMsk, MoscowTimeZone.Get());
    }

    private static string FormatExpiryDate(DateTime expires) =>
        expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

internal static class MoscowTimeZone
{
    private static readonly TimeZoneInfo Fallback = TimeZoneInfo.CreateCustomTimeZone(
        "MSK",
        TimeSpan.FromHours(3),
        "Moscow Standard Time",
        "Moscow Standard Time");

    public static TimeZoneInfo Get()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return Fallback;
        }
        catch (InvalidTimeZoneException)
        {
            return Fallback;
        }
    }
}
