using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Ssh;

namespace SecureTunnelManager.Infrastructure.Services;

public class SshTunnelService : ISshTunnelService
{
    private readonly ICredentialService _credentialService;
    private readonly ILogger<SshTunnelService> _logger;
    private readonly ConcurrentDictionary<int, TunnelSession> _sessions = new();

    public SshTunnelService(ICredentialService credentialService, ILogger<SshTunnelService> logger)
    {
        _credentialService = credentialService;
        _logger = logger;
    }

    public async Task StartAsync(TunnelProfile profile, CancellationToken cancellationToken = default)
    {
        var session = _sessions.GetOrAdd(profile.Id, _ => new TunnelSession(profile.Id));

        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session.Status = TunnelStatus.Connecting;
            session.ErrorMessage = null;

            session.Connection?.Dispose();
            session.Connection = new SshTunnelConnection(_logger);

            await session.Connection.ConnectAsync(profile, _credentialService, cancellationToken).ConfigureAwait(false);
            session.Status = TunnelStatus.Connected;
        }
        catch (Exception ex)
        {
            session.Status = TunnelStatus.Error;
            session.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to start tunnel {ProfileId}", profile.Id);
            throw;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task StopAsync(int profileId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(profileId, out var session))
            return;

        await session.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (session.Connection is not null)
            {
                await session.Connection.DisconnectAsync().ConfigureAwait(false);
                session.Connection.Dispose();
                session.Connection = null;
            }

            session.Status = TunnelStatus.Stopped;
            session.ErrorMessage = null;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public TunnelStatus GetStatus(int profileId)
        => _sessions.TryGetValue(profileId, out var session) ? session.Status : TunnelStatus.Stopped;

    public string? GetErrorMessage(int profileId)
        => _sessions.TryGetValue(profileId, out var session) ? session.ErrorMessage : null;

    public bool IsHealthy(int profileId)
        => _sessions.TryGetValue(profileId, out var session) && session.Connection?.IsConnected == true;

    private sealed class TunnelSession
    {
        public TunnelSession(int profileId) => ProfileId = profileId;
        public int ProfileId { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public SshTunnelConnection? Connection { get; set; }
        public TunnelStatus Status { get; set; } = TunnelStatus.Stopped;
        public string? ErrorMessage { get; set; }
    }
}
