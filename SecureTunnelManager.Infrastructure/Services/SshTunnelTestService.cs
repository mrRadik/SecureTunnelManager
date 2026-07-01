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
    private static readonly TimeSpan ServiceProbeTimeout = TimeSpan.FromSeconds(4);
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
        SshHopChain? chain = null;
        ForwardedPortLocal? testForward = null;
        var testLocalPort = 0;

        try
        {
            chain = await SshHopChain.ConnectAsync(
                request.Profile,
                _credentialService,
                _resilience,
                request.JumpAuthOverrides,
                request.TargetAuthOverride,
                cancellationToken).ConfigureAwait(false);

            testLocalPort = SshHopChain.GetFreeTcpPort();
            testForward = new ForwardedPortLocal(
                "127.0.0.1",
                (uint)testLocalPort,
                request.Profile.RemoteHost,
                (uint)request.Profile.RemotePort);

            chain.TargetClient.AddForwardedPort(testForward);
            testForward.Start();

            var serviceReachable = await ProbeLocalForwardAsync(testLocalPort, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var endpoint = $"{request.Profile.RemoteHost}:{request.Profile.RemotePort}";
            if (serviceReachable)
            {
                return TunnelTestResult.Succeeded(
                    $"Connection successful. SSH route works and {endpoint} is reachable ({stopwatch.Elapsed.TotalSeconds:F1}s).",
                    stopwatch.Elapsed,
                    serviceReachable: true);
            }

            return TunnelTestResult.Succeeded(
                $"SSH route works, but {endpoint} did not respond. The service may be stopped or listening only on localhost ({stopwatch.Elapsed.TotalSeconds:F1}s).",
                stopwatch.Elapsed,
                serviceReachable: false);
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

                    chain?.TargetClient?.RemoveForwardedPort(testForward);
                    testForward.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to clean up tunnel test forward on port {Port}", testLocalPort);
                }
            }

            chain?.Dispose();
        }
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
        catch
        {
            return false;
        }
    }
}
