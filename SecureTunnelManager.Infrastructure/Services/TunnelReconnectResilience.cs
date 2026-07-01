using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Services;

/// <summary>
/// Per-tunnel Polly resilience for session-level reconnects (circuit breaker + configurable backoff).
/// </summary>
public sealed class TunnelReconnectResilience
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TunnelReconnectResilience> _logger;

    public TunnelReconnectResilience(
        ISettingsService settingsService,
        ILogger<TunnelReconnectResilience> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task ExecuteConnectAsync(
        int profileId,
        Func<CancellationToken, Task> connect,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var pipeline = BuildProfilePipeline(profileId, settings);
        await pipeline.ExecuteAsync(
            ct => new ValueTask(connect(ct)),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimeSpan> GetReconnectDelayAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var seconds = Math.Clamp(settings.ReconnectIntervalSeconds, 5, 300);
        return TimeSpan.FromSeconds(seconds);
    }

    public async Task<TimeSpan> GetCircuitBreakerWaitAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var seconds = Math.Clamp(settings.CircuitBreakerBreakSeconds, 30, 600);
        return TimeSpan.FromSeconds(seconds);
    }

    private ResiliencePipeline BuildProfilePipeline(int profileId, Core.Models.AppSettings settings)
    {
        var breakSeconds = Math.Clamp(settings.CircuitBreakerBreakSeconds, 30, 600);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(3),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(Ssh.SshConnectionExceptions.IsTransient),
                OnRetry = args =>
                {
                    _logger.LogInformation(
                        args.Outcome.Exception,
                        "Tunnel {ProfileId} connect retry {Attempt}/2",
                        profileId,
                        args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromMinutes(5),
                BreakDuration = TimeSpan.FromSeconds(breakSeconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(Ssh.SshConnectionExceptions.IsTransient),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Reconnect circuit breaker opened for tunnel {ProfileId} — pausing for {Seconds}s",
                        profileId,
                        breakSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Reconnect circuit breaker closed for tunnel {ProfileId}", profileId);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
