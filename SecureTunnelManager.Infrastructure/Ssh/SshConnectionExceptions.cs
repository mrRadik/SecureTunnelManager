using System.Net.Sockets;
using Renci.SshNet.Common;

namespace SecureTunnelManager.Infrastructure.Ssh;

internal static class SshConnectionExceptions
{
    public static bool IsTransient(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SshAuthenticationException)
                return false;

            if (current is SshConnectionException or SocketException or IOException or TimeoutException)
                return true;
        }

        return false;
    }
}
