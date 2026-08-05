using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Ssh;

/// <summary>
/// SSH hop chain with a local forward to an RDP endpoint on the last hop.
/// </summary>
internal sealed class SshRdpConnection : IDisposable
{
    private SshHopChain? _hopChain;
    private ForwardedPortLocal? _localForwardPort;
    private readonly ILogger _logger;
    private readonly SshResiliencePolicyProvider _resilience;

    public SshRdpConnection(ILogger logger, SshResiliencePolicyProvider resilience)
    {
        _logger = logger;
        _resilience = resilience;
    }

    public int BoundLocalPort { get; private set; }
    public string BoundLocalAddress { get; private set; } = "127.0.0.1";

    public bool IsConnected =>
        _hopChain?.LastHopClient.IsConnected == true &&
        _localForwardPort?.IsStarted == true;

    public async Task ConnectAsync(
        RdpTarget target,
        ICredentialService credentialService,
        CancellationToken cancellationToken)
    {
        await StopInternalAsync().ConfigureAwait(false);

        if (target.JumpHosts.Count == 0)
            throw new InvalidOperationException("At least one jump host is required.");

        if (string.IsNullOrWhiteSpace(target.RdpHost))
            throw new InvalidOperationException("RDP host is required.");

        if (target.RdpPort is < 1 or > 65535)
            throw new InvalidOperationException("RDP port must be between 1 and 65535.");

        _hopChain = await SshHopChain.ConnectHopsAsync(
            target.JumpHosts,
            credentialService,
            _resilience,
            cancellationToken).ConfigureAwait(false);

        BoundLocalAddress = string.IsNullOrWhiteSpace(target.LocalBindAddress) ? "127.0.0.1" : target.LocalBindAddress.Trim();
        BoundLocalPort = target.LocalPort > 0 ? target.LocalPort : SshHopChain.GetFreeTcpPort();

        _localForwardPort = new ForwardedPortLocal(
            BoundLocalAddress,
            (uint)BoundLocalPort,
            target.RdpHost.Trim(),
            (uint)target.RdpPort);

        _hopChain.LastHopClient.AddForwardedPort(_localForwardPort);
        _localForwardPort.Start();

        var hopChain = string.Join(" -> ", target.JumpHosts.Select(h => $"{h.Username}@{h.Host}"));
        _logger.LogInformation(
            "RDP forward started for {Name}: {Bind}:{LocalPort} -> {RdpHost}:{RdpPort} via {HopChain}",
            target.Name,
            BoundLocalAddress,
            BoundLocalPort,
            target.RdpHost,
            target.RdpPort,
            hopChain);
    }

    public Task DisconnectAsync()
    {
        _logger.LogInformation("RDP forward stopped");
        return StopInternalAsync();
    }

    private Task StopInternalAsync()
    {
        try
        {
            if (_localForwardPort is not null)
            {
                if (_localForwardPort.IsStarted)
                    _localForwardPort.Stop();

                try
                {
                    _hopChain?.LastHopClient.RemoveForwardedPort(_localForwardPort);
                }
                catch
                {
                    // Chain may already be disposed.
                }

                _localForwardPort.Dispose();
                _localForwardPort = null;
            }

            _hopChain?.Dispose();
            _hopChain = null;
            BoundLocalPort = 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping RDP SSH connection");
        }

        return Task.CompletedTask;
    }

    public void Dispose() => StopInternalAsync().GetAwaiter().GetResult();
}
