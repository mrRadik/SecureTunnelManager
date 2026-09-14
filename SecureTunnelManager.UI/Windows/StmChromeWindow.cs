using System.Windows;
using System.Windows.Controls;
using SecureTunnelManager.UI.Helpers;

namespace SecureTunnelManager.UI.Windows;

public class StmChromeWindow : Window
{
    public static readonly DependencyProperty ShowFrameBorderProperty =
        DependencyProperty.Register(
            nameof(ShowFrameBorder),
            typeof(bool),
            typeof(StmChromeWindow),
            new PropertyMetadata(true));

    private bool _frameBorderApplied;

    public bool ShowFrameBorder
    {
        get => (bool)GetValue(ShowFrameBorderProperty);
        set => SetValue(ShowFrameBorderProperty, value);
    }

    public StmChromeWindow()
    {
        WindowStyle = WindowStyle.None;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 14;

        AppIconHelper.ApplyWindowIcon(this);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        StateChanged += OnStateChanged;
        Activated += OnActivationChanged;
        Deactivated += OnActivationChanged;
    }

    private void OnActivationChanged(object? sender, EventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        AppIconHelper.ApplyWindowIcon(this);
        NativeWindowHelper.ApplyBorderlessChrome(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyFrameBorderIfNeeded();
        NativeWindowHelper.RefreshBorderlessChrome(this);
    }

    private void ApplyFrameBorderIfNeeded()
    {
        if (!ShowFrameBorder || _frameBorderApplied || Content is not UIElement content)
            return;

        _frameBorderApplied = true;
        Content = null;
        Content = new Border
        {
            BorderBrush = TryFindResource("StmBorderBrush") as System.Windows.Media.Brush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x3C, 0x48)),
            BorderThickness = new Thickness(1),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Child = content
        };
    }

    private void OnContentRendered(object? sender, EventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);

    private void OnStateChanged(object? sender, EventArgs e) =>
        NativeWindowHelper.RefreshBorderlessChrome(this);
}
