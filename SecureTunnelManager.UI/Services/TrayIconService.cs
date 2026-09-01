using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly IVaultService _vaultService;
    private readonly ITunnelManagerService _tunnelManager;
    private readonly ILocalizationService _localization;
    private Window? _menuWindow;
    private LowLevelMouseHook? _mouseHook;

    public TrayIconService(
        IVaultService vaultService,
        ITunnelManagerService tunnelManager,
        ILocalizationService localization)
    {
        _vaultService = vaultService;
        _tunnelManager = tunnelManager;
        _localization = localization;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Visible = true
        };

        TrySetIcon();
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _localization.LanguageChanged += (_, _) => CloseTrayMenu();
        RebuildMenuText();
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    public void ShowBalloon(string title, string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        var icon = severity switch
        {
            NotificationSeverity.Error => System.Windows.Forms.ToolTipIcon.Error,
            NotificationSeverity.Warning => System.Windows.Forms.ToolTipIcon.Warning,
            _ => System.Windows.Forms.ToolTipIcon.Info
        };

        _notifyIcon.ShowBalloonTip(5000, title, message, icon);
    }

    private void TrySetIcon()
    {
        try
        {
            _notifyIcon.Icon = AppIconHelper.LoadNotifyIcon();
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }
    }

    private void OnNotifyIconMouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button == System.Windows.Forms.MouseButtons.Right)
        {
            var cursor = System.Windows.Forms.Control.MousePosition;
            ShowContextMenu(cursor.X, cursor.Y);
        }
    }

    private void ShowContextMenu(int screenX, int screenY)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        app.Dispatcher.BeginInvoke(() =>
        {
            CloseTrayMenu();

            var menuPanel = BuildMenuPanel();
            _menuWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = menuPanel,
            };

            _menuWindow.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CloseTrayMenu();
                    e.Handled = true;
                }
            };

            _menuWindow.Closed += OnMenuWindowClosed;

            _menuWindow.Show();
            _menuWindow.UpdateLayout();

            var anchor = ConvertScreenPointToWpf(screenX, screenY);
            _menuWindow.Left = anchor.X;
            _menuWindow.Top = anchor.Y - _menuWindow.ActualHeight;

            _mouseHook = LowLevelMouseHook.TryInstall(ShouldCloseMenuOnMouseDown, () =>
            {
                app.Dispatcher.BeginInvoke(CloseTrayMenu, DispatcherPriority.Background);
            });
        }, DispatcherPriority.ContextIdle);
    }

    private bool ShouldCloseMenuOnMouseDown()
    {
        if (_menuWindow is null)
            return false;

        var cursor = System.Windows.Forms.Control.MousePosition;
        return !WindowBoundsHelper.ContainsPoint(_menuWindow, cursor.X, cursor.Y);
    }

    private static System.Windows.Point ConvertScreenPointToWpf(int screenX, int screenY)
    {
        var app = System.Windows.Application.Current;
        if (app?.MainWindow is { IsLoaded: true } mainWindow
            && PresentationSource.FromVisual(mainWindow) is { CompositionTarget: { } target })
        {
            return target.TransformFromDevice.Transform(new System.Windows.Point(screenX, screenY));
        }

        var reference = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            Width = 1,
            Height = 1,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
        };

        reference.Show();
        reference.UpdateLayout();
        var point = reference.PointFromScreen(new System.Windows.Point(screenX, screenY));
        reference.Close();
        return point;
    }

    private void OnMenuWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
            window.Closed -= OnMenuWindowClosed;

        _mouseHook?.Dispose();
        _mouseHook = null;

        if (ReferenceEquals(sender, _menuWindow))
            _menuWindow = null;
    }

    private Border BuildMenuPanel()
    {
        var app = System.Windows.Application.Current;
        var panelStyle = app?.TryFindResource("StmTrayMenuPanel") as Style;
        var buttonStyle = app?.TryFindResource("StmTrayMenuButton") as Style;
        var separatorStyle = app?.TryFindResource("StmMenuSeparator") as Style;

        var panel = new Border();
        if (panelStyle is not null)
            panel.Style = panelStyle;

        var items = new StackPanel();
        items.Children.Add(CreateMenuButton("Tray.Open", buttonStyle, () => OpenRequested?.Invoke(this, EventArgs.Empty)));
        items.Children.Add(CreateMenuButton("Tray.StartAll", buttonStyle, () => _ = _tunnelManager.StartAllAsync()));
        items.Children.Add(CreateMenuButton("Tray.StopAll", buttonStyle, () => _ = _tunnelManager.StopAllAsync()));
        items.Children.Add(CreateSeparator(separatorStyle));
        items.Children.Add(CreateMenuButton("Tray.LockVault", buttonStyle, () => _vaultService.Lock(manual: true)));
        items.Children.Add(CreateSeparator(separatorStyle));
        items.Children.Add(CreateMenuButton("Tray.Exit", buttonStyle, () => ExitRequested?.Invoke(this, EventArgs.Empty)));

        panel.Child = items;
        return panel;
    }

    private System.Windows.Controls.Button CreateMenuButton(string key, Style? style, Action action)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = _localization.Get(key),
        };

        if (style is not null)
            button.Style = style;

        button.Click += (_, _) =>
        {
            CloseTrayMenu();
            action();
        };

        return button;
    }

    private static Separator CreateSeparator(Style? style)
    {
        var separator = new Separator();
        if (style is not null)
            separator.Style = style;

        return separator;
    }

    private void RebuildMenuText()
    {
        _notifyIcon.Text = _localization.Get("App.Title");
    }

    private void CloseTrayMenu()
    {
        _mouseHook?.Dispose();
        _mouseHook = null;

        if (_menuWindow is null)
            return;

        _menuWindow.Close();
    }

    public void Dispose()
    {
        CloseTrayMenu();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
