using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.UI.Services;

public sealed class NotificationService : INotificationService
{
    private const int MaxItems = 50;
    private readonly List<AppNotification> _items = new();

    public IReadOnlyList<AppNotification> Items => _items;

    public int UnreadCount => _items.Count(i => !i.IsRead);

    public event EventHandler? Changed;

    public event EventHandler<AppNotification>? Published;

    public void Publish(AppNotification notification)
    {
        _items.Insert(0, notification);
        while (_items.Count > MaxItems)
            _items.RemoveAt(_items.Count - 1);

        Published?.Invoke(this, notification);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkRead(Guid id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null || item.IsRead)
            return;

        item.IsRead = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkAllRead()
    {
        var changed = false;
        foreach (var item in _items.Where(i => !i.IsRead))
        {
            item.IsRead = true;
            changed = true;
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAll()
    {
        if (_items.Count == 0)
            return;

        _items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
