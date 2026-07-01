using Renci.SshNet;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Ssh;

/// <summary>
/// Connects through one or more jump hosts to the target SSH server.
/// </summary>
internal sealed class SshHopChain : IDisposable
{
    private readonly List<SshClient> _hopClients = new();
    private readonly List<ForwardedPortLocal> _hopForwards = new();

    public SshClient TargetClient { get; private set; } = null!;

    public static Task<SshHopChain> ConnectAsync(
        TunnelProfile profile,
        ICredentialService credentialService,
        CancellationToken cancellationToken) =>
        ConnectAsync(profile, credentialService, jumpAuthOverrides: null, targetAuthOverride: null, cancellationToken);

    public static async Task<SshHopChain> ConnectAsync(
        TunnelProfile profile,
        ICredentialService credentialService,
        IReadOnlyList<TunnelAuthOverride>? jumpAuthOverrides,
        TunnelAuthOverride? targetAuthOverride,
        CancellationToken cancellationToken)
    {
        var chain = new SshHopChain();
        await chain.ConnectInternalAsync(profile, credentialService, jumpAuthOverrides, targetAuthOverride, cancellationToken).ConfigureAwait(false);
        return chain;
    }

    private async Task ConnectInternalAsync(
        TunnelProfile profile,
        ICredentialService credentialService,
        IReadOnlyList<TunnelAuthOverride>? jumpAuthOverrides,
        TunnelAuthOverride? targetAuthOverride,
        CancellationToken cancellationToken)
    {
        var hops = profile.GetEffectiveJumpHosts();
        if (hops.Count == 0)
            throw new InvalidOperationException("At least one jump host is required.");

        var targetAuth = await SshConnectionFactory.BuildAuthMethodsAsync(
            profile.TargetAuthMethod,
            profile.TargetUsername,
            profile.TargetCredentialId,
            profile.TargetPrivateKeyPath,
            profile.TargetKeyPassphraseCredentialId,
            targetAuthOverride?.Password,
            targetAuthOverride?.KeyPassphrase,
            credentialService,
            cancellationToken).ConfigureAwait(false);

        SshClient? previousClient = null;

        for (var i = 0; i < hops.Count; i++)
        {
            var hop = hops[i];
            var hopOverride = jumpAuthOverrides is not null && i < jumpAuthOverrides.Count
                ? jumpAuthOverrides[i]
                : null;

            var hopAuth = await SshConnectionFactory.BuildAuthMethodsAsync(
                hop.AuthMethod,
                hop.Username,
                hop.CredentialId,
                hop.PrivateKeyPath,
                hop.KeyPassphraseCredentialId,
                hopOverride?.Password,
                hopOverride?.KeyPassphrase,
                credentialService,
                cancellationToken).ConfigureAwait(false);

            SshClient client;
            if (i == 0)
            {
                var hopInfo = new ConnectionInfo(hop.Host, hop.Port, hop.Username, hopAuth);
                client = new SshClient(hopInfo) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
            }
            else
            {
                var localPort = GetFreeTcpPort();
                var forward = new ForwardedPortLocal("127.0.0.1", (uint)localPort, hop.Host, (uint)hop.Port);
                previousClient!.AddForwardedPort(forward);
                forward.Start();
                _hopForwards.Add(forward);

                var hopInfo = new ConnectionInfo("127.0.0.1", localPort, hop.Username, hopAuth);
                client = new SshClient(hopInfo) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
            }

            await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
            _hopClients.Add(client);
            previousClient = client;
        }

        var targetLocalPort = GetFreeTcpPort();
        var targetForward = new ForwardedPortLocal("127.0.0.1", (uint)targetLocalPort, profile.TargetHost, (uint)profile.TargetPort);
        previousClient!.AddForwardedPort(targetForward);
        targetForward.Start();
        _hopForwards.Add(targetForward);

        var targetInfo = new ConnectionInfo("127.0.0.1", targetLocalPort, profile.TargetUsername, targetAuth);
        TargetClient = new SshClient(targetInfo) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
        await Task.Run(() => TargetClient.Connect(), cancellationToken).ConfigureAwait(false);
    }

    internal static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        try
        {
            if (TargetClient is not null)
            {
                if (TargetClient.IsConnected)
                    TargetClient.Disconnect();
                TargetClient.Dispose();
            }

            for (var i = _hopForwards.Count - 1; i >= 0; i--)
            {
                var forward = _hopForwards[i];
                if (forward.IsStarted)
                    forward.Stop();

                foreach (var client in _hopClients)
                    client.RemoveForwardedPort(forward);

                forward.Dispose();
            }

            _hopForwards.Clear();

            for (var i = _hopClients.Count - 1; i >= 0; i--)
            {
                var client = _hopClients[i];
                if (client.IsConnected)
                    client.Disconnect();
                client.Dispose();
            }

            _hopClients.Clear();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
