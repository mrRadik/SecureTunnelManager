namespace SecureTunnelManager.Core.Services;

/// <summary>
/// Windows startup registration for the application.
/// </summary>
public interface IAutoStartService
{
    bool IsRegisteredWithWindows();
    void RegisterWithWindows();
    void UnregisterFromWindows();
}
