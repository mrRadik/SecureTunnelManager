using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class AppStatusBar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty VaultStatusTextProperty =
        DependencyProperty.Register(nameof(VaultStatusText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsVaultUnlockedProperty =
        DependencyProperty.Register(nameof(IsVaultUnlocked), typeof(bool), typeof(AppStatusBar), new PropertyMetadata(false));

    public static readonly DependencyProperty ActiveConnectionsTextProperty =
        DependencyProperty.Register(nameof(ActiveConnectionsText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionTextProperty =
        DependencyProperty.Register(nameof(VersionText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VaultActionTextProperty =
        DependencyProperty.Register(nameof(VaultActionText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VaultActionCommandProperty =
        DependencyProperty.Register(nameof(VaultActionCommand), typeof(ICommand), typeof(AppStatusBar), new PropertyMetadata(null));

    public static readonly DependencyProperty VaultClickCommandProperty =
        DependencyProperty.Register(nameof(VaultClickCommand), typeof(ICommand), typeof(AppStatusBar), new PropertyMetadata(null));

    public static readonly DependencyProperty NotificationCenterProperty =
        DependencyProperty.Register(nameof(NotificationCenter), typeof(NotificationCenterViewModel), typeof(AppStatusBar), new PropertyMetadata(null));

    public AppStatusBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private Window? _ownerWindow;

    public string VaultStatusText
    {
        get => (string)GetValue(VaultStatusTextProperty);
        set => SetValue(VaultStatusTextProperty, value);
    }

    public bool IsVaultUnlocked
    {
        get => (bool)GetValue(IsVaultUnlockedProperty);
        set => SetValue(IsVaultUnlockedProperty, value);
    }

    public string ActiveConnectionsText
    {
        get => (string)GetValue(ActiveConnectionsTextProperty);
        set => SetValue(ActiveConnectionsTextProperty, value);
    }

    public string VersionText
    {
        get => (string)GetValue(VersionTextProperty);
        set => SetValue(VersionTextProperty, value);
    }

    public string VaultActionText
    {
        get => (string)GetValue(VaultActionTextProperty);
        set => SetValue(VaultActionTextProperty, value);
    }

    public ICommand? VaultActionCommand
    {
        get => (ICommand?)GetValue(VaultActionCommandProperty);
        set => SetValue(VaultActionCommandProperty, value);
    }

    public ICommand? VaultClickCommand
    {
        get => (ICommand?)GetValue(VaultClickCommandProperty);
        set => SetValue(VaultClickCommandProperty, value);
    }

    public NotificationCenterViewModel? NotificationCenter
    {
        get => (NotificationCenterViewModel?)GetValue(NotificationCenterProperty);
        set => SetValue(NotificationCenterProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ownerWindow = Window.GetWindow(this);
        if (_ownerWindow is null)
            return;

        _ownerWindow.LocationChanged += OnOwnerWindowLayoutChanged;
        _ownerWindow.SizeChanged += OnOwnerWindowLayoutChanged;
        _ownerWindow.StateChanged += OnOwnerWindowStateChanged;
        _ownerWindow.IsVisibleChanged += OnOwnerWindowVisibilityChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_ownerWindow is null)
            return;

        _ownerWindow.LocationChanged -= OnOwnerWindowLayoutChanged;
        _ownerWindow.SizeChanged -= OnOwnerWindowLayoutChanged;
        _ownerWindow.StateChanged -= OnOwnerWindowStateChanged;
        _ownerWindow.IsVisibleChanged -= OnOwnerWindowVisibilityChanged;
        _ownerWindow = null;
    }

    private void OnOwnerWindowLayoutChanged(object? sender, EventArgs e) => RefreshOpenPopups();

    private void OnOwnerWindowStateChanged(object? sender, EventArgs e)
    {
        if (_ownerWindow?.WindowState == WindowState.Minimized)
            NotificationCenter?.DismissTransientUi();
    }

    private void OnOwnerWindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_ownerWindow is { IsVisible: false })
            NotificationCenter?.DismissTransientUi();
    }

    private void RefreshOpenPopups()
    {
        if (_ownerWindow?.WindowState == WindowState.Minimized)
            return;

        RefreshPopupPlacement(NotificationToastPopup);
        RefreshPopupPlacement(NotificationPopup);
    }

    private static void RefreshPopupPlacement(Popup popup)
    {
        if (!popup.IsOpen)
            return;

        popup.IsOpen = false;
        popup.IsOpen = true;
    }

    private void OnVaultStatusClick(object sender, MouseButtonEventArgs e)
    {
        if (VaultClickCommand?.CanExecute(null) == true)
            VaultClickCommand.Execute(null);
    }

    private void OnToastMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        NotificationCenter?.PauseToastTimerCommand.Execute(null);
    }

    private void OnToastMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        NotificationCenter?.ResumeToastTimerCommand.Execute(null);
    }
}
