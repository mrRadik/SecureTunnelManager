using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface ISshTunnelTestService
{
    Task<TunnelTestResult> TestAsync(TunnelTestRequest request, CancellationToken cancellationToken = default);
}
