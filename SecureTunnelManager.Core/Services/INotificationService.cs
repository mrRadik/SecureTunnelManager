using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// In-app notification center (status bar bell).
/// </summary>
public interface INotificationService
{
    IReadOnlyList<AppNotification> Items { get; }

    int UnreadCount { get; }

    event EventHandler? Changed;

    event EventHandler<AppNotification>? Published;

    void Publish(AppNotification notification);

    void MarkRead(Guid id);

    void MarkAllRead();

    void ClearAll();
}
