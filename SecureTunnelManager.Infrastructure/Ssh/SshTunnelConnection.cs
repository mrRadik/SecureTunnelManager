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
    private SshClient? _forwardingClient;
    private ForwardedPortLocal? _localForwardPort;
    private readonly ILogger _logger;
    private readonly SshResiliencePolicyProvider _resilience;

    public SshTunnelConnection(ILogger logger, SshResiliencePolicyProvider resilience)
    {
        _logger = logger;
        _resilience = resilience;
    }

    public bool IsConnected =>
        _forwardingClient?.IsConnected == true &&
        _localForwardPort?.IsStarted == true;

    public async Task ConnectAsync(
        TunnelProfile profile,
        ICredentialService credentialService,
        CancellationToken cancellationToken)
    {
        await StopInternalAsync().ConfigureAwait(false);

        if (profile.UseTargetSsh)
        {
            _hopChain = await SshHopChain.ConnectAsync(
                profile,
                credentialService,
                _resilience,
                cancellationToken).ConfigureAwait(false);
            _forwardingClient = _hopChain.TargetClient!;
        }
        else
        {
            _hopChain = await SshHopChain.ConnectHopsAsync(
                profile.GetEffectiveJumpHosts(),
                credentialService,
                _resilience,
                cancellationToken).ConfigureAwait(false);
            _forwardingClient = _hopChain.LastHopClient;
        }

        _localForwardPort = new ForwardedPortLocal(
            profile.LocalBindAddress,
            (uint)profile.LocalPort,
            profile.RemoteHost,
            (uint)profile.RemotePort);

        _forwardingClient.AddForwardedPort(_localForwardPort);
        _localForwardPort.Start();

        var hops = profile.GetEffectiveJumpHosts();
        var hopChain = string.Join(" -> ", hops.Select(h => $"{h.Username}@{h.Host}"));
        if (profile.UseTargetSsh)
        {
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
        else
        {
            _logger.LogInformation(
                "Direct forward started for tunnel {Name}: localhost:{LocalPort} -> {RemoteHost}:{RemotePort} via {HopChain}",
                profile.Name,
                profile.LocalPort,
                profile.RemoteHost,
                profile.RemotePort,
                hopChain);
        }
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

                _forwardingClient?.RemoveForwardedPort(_localForwardPort);
                _localForwardPort.Dispose();
                _localForwardPort = null;
            }

            _forwardingClient = null;
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
