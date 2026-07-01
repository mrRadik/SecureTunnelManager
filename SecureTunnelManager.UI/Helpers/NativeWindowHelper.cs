using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SecureTunnelManager.UI.Helpers;

internal static class NativeWindowHelper
{
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmncrpDisabled = 1;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    private const int WmNcCalcSize = 0x0083;
    private const int WmNcHitTest = 0x0084;
    private const int WmNcActivate = 0x0086;
    private const int WmActivate = 0x0006;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmNclButtonDown = 0x00A1;

    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const uint MonitorDefaultToNearest = 2;

    private const int ResizeBorder = 6;
    private const int DarkChromeColor = 0x001E1E1E;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectNative lpRect);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointNative ptReserved;
        public PointNative ptMaxSize;
        public PointNative ptMaxPosition;
        public PointNative ptMinTrackSize;
        public PointNative ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public uint dwFlags;
    }

    public static void ApplyBorderlessChrome(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
            return;

        var source = HwndSource.FromHwnd(handle);
        if (source is not null)
        {
            IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                WndProc(window, hwnd, msg, wParam, lParam, ref handled);

            source.AddHook(Hook);
        }

        ApplyDwmAttributes(handle);
    }

    public static void RefreshBorderlessChrome(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != nint.Zero)
            ApplyDwmAttributes(handle);
    }

    public static void DragWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
            return;

        ReleaseCapture();
        SendMessage(handle, WmNclButtonDown, (nint)HtCaption, nint.Zero);
    }

    private static void ApplyDwmAttributes(IntPtr handle)
    {
        var ncPolicy = DwmncrpDisabled;
        _ = DwmSetWindowAttribute(handle, DwmwaNcRenderingPolicy, ref ncPolicy, sizeof(int));

        var darkMode = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

        var borderColor = DarkChromeColor;
        _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(int));

        var chromeColor = DarkChromeColor;
        _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref chromeColor, sizeof(int));

        var cornerPreference = DwmwcpDoNotRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
    }

    private static IntPtr WndProc(Window window, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmNcActivate:
                handled = true;
                return (nint)1;

            case WmActivate:
                ApplyDwmAttributes(hwnd);
                break;

            case WmNcCalcSize when wParam != nint.Zero:
                handled = true;
                return nint.Zero;

            case WmGetMinMaxInfo:
                ApplyMaximizedBounds(hwnd, lParam, ref handled);
                break;

            case WmNcHitTest when window.ResizeMode == ResizeMode.CanResize && window.WindowState == WindowState.Normal:
                return HandleResizeHitTest(hwnd, lParam, ref handled);
        }

        return nint.Zero;
    }

    private static void ApplyMaximizedBounds(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == nint.Zero)
            return;

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var work = monitorInfo.rcWork;
        var monitorRect = monitorInfo.rcMonitor;

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.ptMaxPosition.X = work.Left - monitorRect.Left;
        minMaxInfo.ptMaxPosition.Y = work.Top - monitorRect.Top;
        minMaxInfo.ptMaxSize.X = work.Right - work.Left;
        minMaxInfo.ptMaxSize.Y = work.Bottom - work.Top;
        Marshal.StructureToPtr(minMaxInfo, lParam, true);
        handled = true;
    }

    private static IntPtr HandleResizeHitTest(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        var lParamValue = lParam.ToInt64();
        var x = (short)(lParamValue & 0xFFFF);
        var y = (short)((lParamValue >> 16) & 0xFFFF);

        GetWindowRect(hwnd, out var rect);

        var left = x < rect.Left + ResizeBorder;
        var right = x >= rect.Right - ResizeBorder;
        var top = y < rect.Top + ResizeBorder;
        var bottom = y >= rect.Bottom - ResizeBorder;

        if (top && left)
        {
            handled = true;
            return (nint)HtTopLeft;
        }

        if (top && right)
        {
            handled = true;
            return (nint)HtTopRight;
        }

        if (bottom && left)
        {
            handled = true;
            return (nint)HtBottomLeft;
        }

        if (bottom && right)
        {
            handled = true;
            return (nint)HtBottomRight;
        }

        if (left)
        {
            handled = true;
            return (nint)HtLeft;
        }

        if (right)
        {
            handled = true;
            return (nint)HtRight;
        }

        if (top)
        {
            handled = true;
            return (nint)HtTop;
        }

        if (bottom)
        {
            handled = true;
            return (nint)HtBottom;
        }

        return nint.Zero;
    }
}
