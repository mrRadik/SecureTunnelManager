namespace SecureTunnelManager.UI.Services;

public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = @"Local\SecureTunnelManager.SingleInstance";
    private const string ActivateEventName = @"Local\SecureTunnelManager.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly CancellationTokenSource _listenerCts = new();

    public SingleInstanceManager()
    {
        _mutex = new Mutex(true, MutexName, out var isFirst);
        IsFirstInstance = isFirst;
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
    }

    public bool IsFirstInstance { get; }

    public void RequestActivation() => _activateEvent.Set();

    public void StartActivationListener(Action onActivateRequested)
    {
        if (!IsFirstInstance)
            return;

        Task.Run(() =>
        {
            while (!_listenerCts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!_activateEvent.WaitOne(500))
                        continue;

                    System.Windows.Application.Current?.Dispatcher.Invoke(onActivateRequested);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, _listenerCts.Token);
    }

    public void Dispose()
    {
        _listenerCts.Cancel();
        _listenerCts.Dispose();
        _activateEvent.Dispose();

        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was not owned by this thread.
            }
        }

        _mutex.Dispose();
    }
}
