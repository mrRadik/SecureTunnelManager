using Microsoft.Win32;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Services;

public class AutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SecureTunnelManager";

    public bool IsRegisteredWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is not null;
    }

    public void RegisterWithWindows()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine application path.");

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open registry Run key.");

        key.SetValue(AppName, $"\"{exePath}\"");
    }

    public void UnregisterFromWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
