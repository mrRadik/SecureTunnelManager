using Renci.SshNet;
using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Infrastructure.Ssh;

internal interface IJumpHostPasswordExpiryProbe
{
    Task ProbeIfNeededAsync(SshClient client, JumpHostHop hop, CancellationToken cancellationToken = default);
}
