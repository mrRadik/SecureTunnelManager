using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Services;

/// <summary>
/// Orchestrates tunnel lifecycle with automatic reconnection, Polly circuit breaker, and backoff.
/// </summary>
public class TunnelManagerService : ITunnelManagerService, IDisposable
{
    private readonly ITunnelProfileService _profileService;
    private readonly SshTunnelService _sshTunnelService;
    private readonly TunnelReconnectResilience _reconnectResilience;
    private readonly ILogger<TunnelManagerService> _logger;
    private readonly ConcurrentDictionary<int, TunnelWorker> _workers = new();
    private readonly ConcurrentDictionary<int, TunnelRuntimeState> _states = new();
    private readonly ConcurrentDictionary<int, int> _loopGenerations = new();

    public TunnelManagerService(
        ITunnelProfileService profileService,
        SshTunnelService sshTunnelService,
        TunnelReconnectResilience reconnectResilience,
        ILogger<TunnelManagerService> logger)
    {
        _profileService = profileService;
        _sshTunnelService = sshTunnelService;
        _reconnectResilience = reconnectResilience;
        _logger = logger;
    }

    public event EventHandler<TunnelRuntimeState>? TunnelStateChanged;

    public async Task StartTunnelAsync(int profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileService.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile {profileId} not found.");

        EnsureState(profile);
        await StopWorkerAsync(profileId).ConfigureAwait(false);

        var generation = BumpLoopGeneration(profileId);
        var worker = new TunnelWorker(profileId, generation, this);
        _workers[profileId] = worker;
        worker.Start();
    }

    public async Task StopTunnelAsync(int profileId, CancellationToken cancellationToken = default)
    {
        BumpLoopGeneration(profileId);
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
        {
            if (_workers.ContainsKey(profile.Id))
                continue;

            await StartTunnelAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var ids = _workers.Keys.ToList();
        foreach (var id in ids)
            await StopTunnelAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestartAllAsync(CancellationToken cancellationToken = default)
    {
        var ids = _workers.Keys.ToList();
        foreach (var id in ids)
            await RestartTunnelAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<TunnelRuntimeState> GetRuntimeStates() => _states.Values.OrderBy(s => s.Name).ToList();

    public TunnelRuntimeState? GetRuntimeState(int profileId)
        => _states.TryGetValue(profileId, out var state) ? state : null;

    internal async Task RunTunnelLoopAsync(int profileId, int generation, CancellationToken cancellationToken)
    {
        var attempt = 0;
        string? lastError = null;

        while (!cancellationToken.IsCancellationRequested && IsLoopGenerationCurrent(profileId, generation))
        {
            var profile = await _profileService.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                lastError = "Profile not found";
                if (!TryUpdateState(profileId, generation, TunnelStatus.Error, lastError, reconnectAttempt: attempt))
                    break;
                break;
            }

            EnsureState(profile);
            if (!TryUpdateState(profileId, generation, TunnelStatus.Connecting, lastError, reconnectAttempt: attempt))
                break;

            try
            {
                await _reconnectResilience.ExecuteConnectAsync(
                    profileId,
                    ct => _sshTunnelService.StartAsync(profile, ct),
                    cancellationToken).ConfigureAwait(false);

                if (!TryUpdateState(profileId, generation, TunnelStatus.Connected, error: null, reconnectAttempt: 0))
                    break;
                attempt = 0;
                lastError = null;

                while (!cancellationToken.IsCancellationRequested && IsLoopGenerationCurrent(profileId, generation))
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
            catch (BrokenCircuitException)
            {
                lastError = BuildCircuitBreakerMessage(lastError);
                _logger.LogWarning("Tunnel {ProfileId} reconnect circuit open", profileId);
                if (!TryUpdateState(profileId, generation, TunnelStatus.Error, lastError, reconnectAttempt: attempt))
                    break;

                try
                {
                    var wait = await _reconnectResilience.GetCircuitBreakerWaitAsync(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tunnel {ProfileId} error", profileId);
                lastError = ex.Message;
                if (!TryUpdateState(profileId, generation, TunnelStatus.Error, ex.Message, reconnectAttempt: attempt))
                    break;
            }

            if (cancellationToken.IsCancellationRequested || !IsLoopGenerationCurrent(profileId, generation))
                break;

            attempt++;
            var delay = await _reconnectResilience.GetReconnectDelayAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Reconnect attempt {Attempt} for tunnel {ProfileId} in {Delay}s",
                attempt,
                profileId,
                delay.TotalSeconds);
            if (!TryUpdateState(profileId, generation, TunnelStatus.Error, lastError, reconnectAttempt: attempt))
                break;

            try
            {
                await _sshTunnelService.StopAsync(profileId, cancellationToken).ConfigureAwait(false);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        if (IsLoopGenerationCurrent(profileId, generation))
            await _sshTunnelService.StopAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!cancellationToken.IsCancellationRequested && IsLoopGenerationCurrent(profileId, generation))
            UpdateState(profileId, TunnelStatus.Stopped, error: null, reconnectAttempt: 0);
    }

    private static string BuildCircuitBreakerMessage(string? lastError)
    {
        const string summary = "Connection failed. Automatic reconnect paused (circuit breaker).";
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

        state.DestinationDisplay = profile.UseTargetSsh
            ? $"{profile.TargetUsername}@{profile.TargetHost}:{profile.RemotePort}"
            : $"{profile.RemoteHost}:{profile.RemotePort}";
        state.TargetDisplay = profile.UseTargetSsh
            ? $"{profile.TargetUsername}@{profile.TargetHost}"
            : profile.RemoteHost;
        state.LocalPort = profile.LocalPort;
    }

    private int BumpLoopGeneration(int profileId) =>
        _loopGenerations.AddOrUpdate(profileId, 1, static (_, generation) => generation + 1);

    private bool IsLoopGenerationCurrent(int profileId, int generation) =>
        _loopGenerations.TryGetValue(profileId, out var current) && current == generation;

    private bool TryUpdateState(
        int profileId,
        int generation,
        TunnelStatus status,
        string? error,
        int reconnectAttempt)
    {
        if (!IsLoopGenerationCurrent(profileId, generation))
            return false;

        UpdateState(profileId, status, error, reconnectAttempt);
        return true;
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

        public TunnelWorker(int profileId, int generation, TunnelManagerService manager)
        {
            _task = manager.RunTunnelLoopAsync(profileId, generation, _cts.Token);
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
