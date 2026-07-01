using System.Windows;
using SecureTunnelManager.UI.Helpers;

namespace SecureTunnelManager.UI.Windows;

public class StmChromeWindow : Window
{
    public StmChromeWindow()
    {
        WindowStyle = WindowStyle.None;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 14;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        StateChanged += OnStateChanged;
        Activated += OnActivationChanged;
        Deactivated += OnActivationChanged;
    }

    private void OnActivationChanged(object? sender, EventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeWindowHelper.ApplyBorderlessChrome(this);

    private void OnLoaded(object? sender, RoutedEventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);

    private void OnContentRendered(object? sender, EventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);

    private void OnStateChanged(object? sender, EventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);
}
