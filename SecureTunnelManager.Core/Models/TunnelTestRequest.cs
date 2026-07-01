namespace SecureTunnelManager.Core.Models;

public class TunnelAuthOverride
{
    public string? Password { get; init; }
    public string? KeyPassphrase { get; init; }
}

public class TunnelTestRequest
{
    public required TunnelProfile Profile { get; init; }
    public IReadOnlyList<TunnelAuthOverride> JumpAuthOverrides { get; init; } = Array.Empty<TunnelAuthOverride>();
    public TunnelAuthOverride TargetAuthOverride { get; init; } = new();
}

public sealed class TunnelTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool SshRouteOk { get; init; }
    public bool ServiceReachable { get; init; }

    public static TunnelTestResult Failed(string message, TimeSpan duration) => new()
    {
        Success = false,
        Message = message,
        Duration = duration
    };

    public static TunnelTestResult Succeeded(string message, TimeSpan duration, bool serviceReachable) => new()
    {
        Success = true,
        Message = message,
        Duration = duration,
        SshRouteOk = true,
        ServiceReachable = serviceReachable
    };
}
