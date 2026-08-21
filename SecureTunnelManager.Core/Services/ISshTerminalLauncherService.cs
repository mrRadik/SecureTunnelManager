using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Opens an external terminal with a pre-built ssh command for a tunnel profile.
/// </summary>
public interface ISshTerminalLauncherService
{
    SshTerminalLaunchResult Launch(TunnelProfile profile);
}

public sealed class SshTerminalLaunchResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static SshTerminalLaunchResult Ok() => new() { Success = true };

    public static SshTerminalLaunchResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
