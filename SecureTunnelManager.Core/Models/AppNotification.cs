namespace SecureTunnelManager.Core.Models;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public enum NotificationActionKind
{
    EditTunnel,
    EditRdpTarget,
    UnlockVault,
    OpenSettings
}

/// <summary>
/// In-app notification shown in the status bar notification center.
/// </summary>
public sealed class AppNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;

    public string MessageKey { get; init; } = string.Empty;

    public object[] MessageArgs { get; init; } = Array.Empty<object>();

    public bool IsRead { get; set; }

    public NotificationActionKind? ActionKind { get; init; }

    public int? ResourceId { get; init; }

    public string? ActionLabelKey { get; init; }
}
