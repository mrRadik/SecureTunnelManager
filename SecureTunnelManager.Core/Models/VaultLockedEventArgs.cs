namespace SecureTunnelManager.Core.Models;

public sealed class VaultLockedEventArgs : EventArgs
{
    public VaultLockedEventArgs(bool isManual)
    {
        IsManual = isManual;
    }

    public bool IsManual { get; }
}
