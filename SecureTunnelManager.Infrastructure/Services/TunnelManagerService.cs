using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Services;

/// <summary>
/// Orchestrates tunnel lifecycle with automatic reconnection and exponential backoff.
/// </summary>
public class TunnelManagerService : ITunnelManagerService, IDisposable
{
    private const int MaxReconnectAttempts = 3;
    private static readonly int[] BackoffSeconds = [5, 15, 30];

    private readonly ITunnelProfileService _profileService;
    private readonly SshTunnelService _sshTunnelService;
    private readonly ILogger<TunnelManagerService> _logger;
    private readonly ConcurrentDictionary<int, TunnelWorker> _workers = new();
    private readonly ConcurrentDictionary<int, TunnelRuntimeState> _states = new();

    public TunnelManagerService(
        ITunnelProfileService profileService,
        SshTunnelService sshTunnelService,
        ILogger<TunnelManagerService> logger)
    {
        _profileService = profileService;
        _sshTunnelService = sshTunnelService;
        _logger = logger;
    }

    public event EventHandler<TunnelRuntimeState>? TunnelStateChanged;

    public async Task StartTunnelAsync(int profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileService.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile {profileId} not found.");

        EnsureState(profile);
        await StopWorkerAsync(profileId).ConfigureAwait(false);

        var worker = new TunnelWorker(profileId, this);
        _workers[profileId] = worker;
        worker.Start();
    }

    public async Task StopTunnelAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await StopWorkerAsync(profileId).ConfigureAwait(false);
        await _sshTunnelService.StopAsync(profileId, cancellationToken).ConfigureAwait(false);
        UpdateState(profileId, TunnelStatus.Stopped, error: null, reconnectAttempt: 0);
    }

    public async Task RestartTunnelAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await StopTunnelAsync(profileId, cancellationToken).ConfigureAwait(false);
        await StartTunnelAsync(profileId, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _profileService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var profile in profiles)
            await StartTunnelAsync(profile.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var ids = _workers.Keys.ToList();
        foreach (var id in ids)
            await StopTunnelAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<TunnelRuntimeState> GetRuntimeStates() => _states.Values.OrderBy(s => s.Name).ToList();

    public TunnelRuntimeState? GetRuntimeState(int profileId)
        => _states.TryGetValue(profileId, out var state) ? state : null;

    internal async Task RunTunnelLoopAsync(int profileId, CancellationToken cancellationToken)
    {
        var attempt = 0;
        string? lastError = null;
        var stoppedAfterMaxRetries = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var profile = await _profileService.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                lastError = "Profile not found";
                UpdateState(profileId, TunnelStatus.Error, lastError, reconnectAttempt: attempt);
                break;
            }

            EnsureState(profile);
            UpdateState(profileId, TunnelStatus.Connecting, error: null, reconnectAttempt: attempt);

            try
            {
                await _sshTunnelService.StartAsync(profile, cancellationToken).ConfigureAwait(false);
                UpdateState(profileId, TunnelStatus.Connected, error: null, reconnectAttempt: 0);
                attempt = 0;
                lastError = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                    if (!_sshTunnelService.IsHealthy(profileId))
                    {
                        lastError = "Connection lost";
                        _logger.LogWarning("Tunnel {ProfileId} connection lost", profileId);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tunnel {ProfileId} error", profileId);
                lastError = ex.Message;
                UpdateState(profileId, TunnelStatus.Error, ex.Message, reconnectAttempt: attempt);
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            attempt++;
            if (attempt > MaxReconnectAttempts)
            {
                stoppedAfterMaxRetries = true;
                var message = BuildReconnectLimitMessage(lastError);
                _logger.LogWarning(
                    "Tunnel {ProfileId} stopped after {Attempts} reconnect attempts",
                    profileId,
                    MaxReconnectAttempts);
                UpdateState(profileId, TunnelStatus.Error, message, reconnectAttempt: 0);
                break;
            }

            var delayIndex = Math.Min(attempt - 1, BackoffSeconds.Length - 1);
            var delay = BackoffSeconds[delayIndex];
            _logger.LogInformation("Reconnect attempt {Attempt} for tunnel {ProfileId} in {Delay}s", attempt, profileId, delay);
            UpdateState(profileId, TunnelStatus.Connecting, lastError, reconnectAttempt: attempt);

            try
            {
                await _sshTunnelService.StopAsync(profileId, CancellationToken.None).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        await _sshTunnelService.StopAsync(profileId, CancellationToken.None).ConfigureAwait(false);
        if (!cancellationToken.IsCancellationRequested && !stoppedAfterMaxRetries)
            UpdateState(profileId, TunnelStatus.Stopped, error: null, reconnectAttempt: 0);
    }

    private static string BuildReconnectLimitMessage(string? lastError)
    {
        const string summary = "Tunnel failed after 3 reconnect attempts.";
        return string.IsNullOrWhiteSpace(lastError)
            ? summary
            : $"{summary} {lastError}";
    }

    private void EnsureState(TunnelProfile profile)
    {
        _states.AddOrUpdate(profile.Id,
            _ =>
            {
                var state = new TunnelRuntimeState
                {
                    ProfileId = profile.Id,
                    Status = TunnelStatus.Stopped
                };
                ApplyRoute(state, profile);
                return state;
            },
            (_, existing) =>
            {
                ApplyRoute(existing, profile);
                return existing;
            });
    }

    private static void ApplyRoute(TunnelRuntimeState state, TunnelProfile profile)
    {
        var bind = string.IsNullOrWhiteSpace(profile.LocalBindAddress) ? "127.0.0.1" : profile.LocalBindAddress.Trim();
        state.Name = profile.Name;
        state.LocalEndpoint = $"{bind}:{profile.LocalPort}";

        var hops = profile.GetEffectiveJumpHosts();
        state.JumpHostDisplays = hops.Select(hop =>
            string.IsNullOrWhiteSpace(hop.Host)
                ? "—"
                : hop.Port == 22 ? hop.Host.Trim() : $"{hop.Host.Trim()}:{hop.Port}").ToList();
        state.JumpHostDisplay = state.JumpHostDisplays.Count switch
        {
            0 => "—",
            1 => state.JumpHostDisplays[0],
            _ => string.Join(" → ", state.JumpHostDisplays)
        };

        state.DestinationDisplay = $"{profile.TargetUsername}@{profile.TargetHost}:{profile.RemotePort}";
        state.TargetDisplay = $"{profile.TargetUsername}@{profile.TargetHost}";
        state.LocalPort = profile.LocalPort;
    }

    private void UpdateState(int profileId, TunnelStatus status, string? error, int reconnectAttempt)
    {
        if (!_states.TryGetValue(profileId, out var state))
            return;

        if (status == TunnelStatus.Connected)
            state.LastConnectedAt = DateTime.UtcNow;

        state.Status = status;
        state.ErrorMessage = error;
        state.ReconnectAttempt = reconnectAttempt;
        TunnelStateChanged?.Invoke(this, state with { });
    }

    private async Task StopWorkerAsync(int profileId)
    {
        if (_workers.TryRemove(profileId, out var worker))
            await worker.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        foreach (var id in _workers.Keys.ToList())
        {
            if (_workers.TryRemove(id, out var worker))
                worker.Dispose();
        }
    }

    private sealed class TunnelWorker : IAsyncDisposable, IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;
        private bool _disposed;

        public TunnelWorker(int profileId, TunnelManagerService manager)
        {
            _task = manager.RunTunnelLoopAsync(profileId, _cts.Token);
        }

        public void Start() { /* task already running */ }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await _cts.CancelAsync().ConfigureAwait(false);
            try
            {
                await _task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the tunnel loop exits after cancellation.
            }
            catch (TimeoutException)
            {
                // Best-effort shutdown; SSH cleanup continues in the background.
            }

            _cts.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
