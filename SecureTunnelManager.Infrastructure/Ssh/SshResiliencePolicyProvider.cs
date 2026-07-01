using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Ssh;

/// <summary>
/// Per-endpoint Polly pipelines: fast retries for transient SSH failures + circuit breaker to avoid storms.
/// </summary>
public sealed class SshResiliencePolicyProvider
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SshResiliencePolicyProvider> _logger;

    public SshResiliencePolicyProvider(
        ISettingsService settingsService,
        ILogger<SshResiliencePolicyProvider> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task ExecuteConnectAsync(
        string host,
        int port,
        Func<CancellationToken, Task> connect,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var pipeline = BuildEndpointPipeline($"{host}:{port}", settings);
        await pipeline.ExecuteAsync(
            ct => new ValueTask(connect(ct)),
            cancellationToken).ConfigureAwait(false);
    }

    private ResiliencePipeline BuildEndpointPipeline(string endpointKey, Core.Models.AppSettings settings)
    {
        var breakSeconds = Math.Clamp(settings.CircuitBreakerBreakSeconds, 30, 600);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(SshConnectionExceptions.IsTransient),
                OnRetry = args =>
                {
                    _logger.LogDebug(
                        args.Outcome.Exception,
                        "SSH connect to {Endpoint} retry {Attempt}/{Max}",
                        endpointKey,
                        args.AttemptNumber,
                        3);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromMinutes(2),
                BreakDuration = TimeSpan.FromSeconds(breakSeconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(SshConnectionExceptions.IsTransient),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Circuit breaker opened for {Endpoint} — pausing connect attempts for {Seconds}s",
                        endpointKey,
                        breakSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Circuit breaker closed for {Endpoint} — connect attempts resumed", endpointKey);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
