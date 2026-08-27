using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Ssh;

namespace SecureTunnelManager.Infrastructure.Services;

public class SshTunnelTestService : ISshTunnelTestService
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ServiceProbeTimeout = TimeSpan.FromSeconds(3);
    private readonly ICredentialService _credentialService;
    private readonly SshResiliencePolicyProvider _resilience;
    private readonly ILogger<SshTunnelTestService> _logger;

    public SshTunnelTestService(
        ICredentialService credentialService,
        SshResiliencePolicyProvider resilience,
        ILogger<SshTunnelTestService> logger)
    {
        _credentialService = credentialService;
        _resilience = resilience;
        _logger = logger;
    }

    public async Task<TunnelTestResult> TestAsync(TunnelTestRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TestTimeout);

        SshHopChain? chain = null;
        SshClient? forwardingClient = null;
        ForwardedPortLocal? testForward = null;
        var testLocalPort = 0;
        var connectOptions = SshConnectOptions.ForQuickTest(TestTimeout, created => chain = created);

        try
        {
            var result = await ExecuteTestAsync(
                    request,
                    connectOptions,
                    timeoutCts.Token,
                    client => forwardingClient = client,
                    (forward, port) =>
                    {
                        testForward = forward;
                        testLocalPort = port;
                    })
                .WaitAsync(timeoutCts.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();
            return TunnelTestResult.Succeeded(result.Endpoint, stopwatch.Elapsed, result.ServiceReachable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Tunnel test timed out after {Timeout}s for {TunnelName}",
                TestTimeout.TotalSeconds,
                request.Profile.Name);
            return TunnelTestResult.Failed("Connection timed out.", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Tunnel test failed for {TunnelName}", request.Profile.Name);
            return TunnelTestResult.Failed(ex.Message, stopwatch.Elapsed);
        }
        finally
        {
            if (testForward is not null)
            {
                try
                {
                    if (testForward.IsStarted)
                        testForward.Stop();

                    forwardingClient?.RemoveForwardedPort(testForward);
                    testForward.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to clean up tunnel test forward on port {Port}", testLocalPort);
                }
            }

            // Dispose aborts a hung SSH handshake so WaitAsync can complete promptly.
            chain?.Dispose();
        }
    }

    private async Task<TunnelTestResult> ExecuteTestAsync(
        TunnelTestRequest request,
        SshConnectOptions connectOptions,
        CancellationToken cancellationToken,
        Action<SshClient> setForwardingClient,
        Action<ForwardedPortLocal, int> setTestForward)
    {
        SshClient forwardingClient;

        if (request.Profile.UseTargetSsh)
        {
            var chain = await SshHopChain.ConnectAsync(
                request.Profile,
                _credentialService,
                _resilience,
                request.JumpAuthOverrides,
                request.TargetAuthOverride,
                cancellationToken,
                connectOptions).ConfigureAwait(false);
            forwardingClient = chain.TargetClient!;
        }
        else
        {
            var chain = await SshHopChain.ConnectHopsAsync(
                request.Profile.GetEffectiveJumpHosts(),
                _credentialService,
                _resilience,
                request.JumpAuthOverrides,
                cancellationToken,
                connectOptions).ConfigureAwait(false);
            forwardingClient = chain.LastHopClient;
        }

        setForwardingClient(forwardingClient);

        var testLocalPort = SshHopChain.GetFreeTcpPort();
        var testForward = new ForwardedPortLocal(
            "127.0.0.1",
            (uint)testLocalPort,
            request.Profile.RemoteHost,
            (uint)request.Profile.RemotePort);

        forwardingClient.AddForwardedPort(testForward);
        testForward.Start();
        setTestForward(testForward, testLocalPort);

        var serviceReachable = await ProbeLocalForwardAsync(testLocalPort, cancellationToken).ConfigureAwait(false);
        var endpoint = $"{request.Profile.RemoteHost}:{request.Profile.RemotePort}";
        return TunnelTestResult.Succeeded(endpoint, TimeSpan.Zero, serviceReachable);
    }

    private static async Task<bool> ProbeLocalForwardAsync(int localPort, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ServiceProbeTimeout);

            using var client = new TcpClient();
            await client.ConnectAsync(System.Net.IPAddress.Loopback, localPort, timeoutCts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
