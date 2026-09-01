using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SecureTunnelManager.UI.Helpers;

internal sealed class LowLevelMouseHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;

    private readonly HookProc _proc;
    private readonly Func<bool> _shouldClose;
    private readonly Action _close;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    private LowLevelMouseHook(Func<bool> shouldClose, Action close)
    {
        _shouldClose = shouldClose;
        _close = close;
        _proc = HookCallback;
        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        _hookId = SetWindowsHookEx(WhMouseLl, _proc, GetModuleHandle(moduleName), 0);
    }

    public static LowLevelMouseHook? TryInstall(Func<bool> shouldClose, Action close)
    {
        try
        {
            return new LowLevelMouseHook(shouldClose, close);
        }
        catch
        {
            return null;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_disposed)
        {
            var message = wParam.ToInt32();
            if (message is WmLButtonDown or WmRButtonDown or WmMButtonDown && _shouldClose())
                _close();
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

internal static class WindowBoundsHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectNative lpRect);

    public static bool ContainsPoint(System.Windows.Window window, int screenX, int screenY)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return false;

        if (!GetWindowRect(handle, out var rect))
            return false;

        return screenX >= rect.Left
            && screenX < rect.Right
            && screenY >= rect.Top
            && screenY < rect.Bottom;
    }
}
