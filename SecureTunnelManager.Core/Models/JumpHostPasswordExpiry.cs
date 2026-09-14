namespace SecureTunnelManager.Core.Models;

public static class JumpHostPasswordExpiry
{
    /// <summary>Stored when <c>net user</c> reports password never expires.</summary>
    public static readonly DateTime NeverExpires = DateTime.MaxValue;
}
