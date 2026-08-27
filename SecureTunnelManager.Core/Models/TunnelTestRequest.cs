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
    public TimeSpan Duration { get; init; }
    public bool SshRouteOk { get; init; }
    public bool ServiceReachable { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string? TechnicalError { get; init; }

    public static TunnelTestResult Failed(string technicalError, TimeSpan duration) => new()
    {
        Success = false,
        Duration = duration,
        TechnicalError = technicalError
    };

    public static TunnelTestResult Succeeded(string endpoint, TimeSpan duration, bool serviceReachable) => new()
    {
        Success = true,
        Duration = duration,
        SshRouteOk = true,
        ServiceReachable = serviceReachable,
        Endpoint = endpoint
    };
}
