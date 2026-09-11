using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core;

/// <summary>
/// Connection identity for jump host deduplication (credentials excluded — each profile had its own vault row).
/// </summary>
public readonly record struct JumpHostConnectionFingerprint(
    string Host,
    int Port,
    string Username,
    AuthMethod AuthMethod,
    string? PrivateKeyPath)
{
    public static JumpHostConnectionFingerprint FromHop(JumpHostHop hop) => new(
        hop.Host.Trim(),
        hop.Port,
        hop.Username.Trim(),
        hop.AuthMethod,
        NormalizePath(hop.PrivateKeyPath));

    public static JumpHostConnectionFingerprint FromJumpHost(JumpHost jumpHost) => new(
        jumpHost.Host.Trim(),
        jumpHost.Port,
        jumpHost.Username.Trim(),
        jumpHost.AuthMethod,
        NormalizePath(jumpHost.PrivateKeyPath));

    private static string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();
}
