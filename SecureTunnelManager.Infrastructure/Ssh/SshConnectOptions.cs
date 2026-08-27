namespace SecureTunnelManager.Infrastructure.Ssh;

internal sealed class SshConnectOptions
{
    public TimeSpan? ConnectionTimeout { get; init; }

    public bool RetryTransientFailures { get; init; } = true;

    public Action<SshHopChain>? OnChainCreated { get; init; }

    public static SshConnectOptions ForQuickTest(TimeSpan timeout, Action<SshHopChain> onChainCreated) => new()
    {
        ConnectionTimeout = timeout,
        RetryTransientFailures = false,
        OnChainCreated = onChainCreated
    };
}
