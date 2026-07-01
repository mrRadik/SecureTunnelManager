using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Ssh;

/// <summary>
/// Manages jump-host chain + target SSH session with local port forwarding.
/// </summary>
internal sealed class SshTunnelConnection : IDisposable
{
    private SshHopChain? _hopChain;
    private ForwardedPortLocal? _localForwardPort;
    private readonly ILogger _logger;

    public SshTunnelConnection(ILogger logger) => _logger = logger;

    public bool IsConnected =>
        _hopChain?.TargetClient.IsConnected == true &&
        _localForwardPort?.IsStarted == true;

    public async Task ConnectAsync(
        TunnelProfile profile,
        ICredentialService credentialService,
        CancellationToken cancellationToken)
    {
        await StopInternalAsync().ConfigureAwait(false);

        _hopChain = await SshHopChain.ConnectAsync(profile, credentialService, cancellationToken).ConfigureAwait(false);

        _localForwardPort = new ForwardedPortLocal(
            profile.LocalBindAddress,
            (uint)profile.LocalPort,
            profile.RemoteHost,
            (uint)profile.RemotePort);

        _hopChain.TargetClient.AddForwardedPort(_localForwardPort);
        _localForwardPort.Start();

        var hops = profile.GetEffectiveJumpHosts();
        var hopChain = string.Join(" -> ", hops.Select(h => $"{h.Username}@{h.Host}"));
        _logger.LogInformation(
            "Connection started for tunnel {Name}: localhost:{LocalPort} -> {RemoteHost}:{RemotePort} via {HopChain} -> {TargetUser}@{TargetHost}",
            profile.Name,
            profile.LocalPort,
            profile.RemoteHost,
            profile.RemotePort,
            hopChain,
            profile.TargetUsername,
            profile.TargetHost);
    }

    public Task DisconnectAsync()
    {
        _logger.LogInformation("Connection stopped");
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

                _hopChain?.TargetClient.RemoveForwardedPort(_localForwardPort);
                _localForwardPort.Dispose();
                _localForwardPort = null;
            }

            _hopChain?.Dispose();
            _hopChain = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping SSH connection");
        }

        return Task.CompletedTask;
    }

    public void Dispose() => StopInternalAsync().GetAwaiter().GetResult();
}
