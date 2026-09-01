using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private ContextMenu? _contextMenu;

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
        _localization.LanguageChanged += (_, _) => RebuildMenu();
        RebuildMenu();
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
            ShowContextMenu();
    }

    private void ShowContextMenu()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        app.Dispatcher.BeginInvoke(() =>
        {
            _contextMenu ??= BuildContextMenu();

            var target = app.MainWindow ?? app.Windows.OfType<Window>().FirstOrDefault();
            if (target is null)
                return;

            _contextMenu.PlacementTarget = target;
            _contextMenu.Placement = PlacementMode.MousePoint;
            _contextMenu.IsOpen = true;
        }, DispatcherPriority.ContextIdle);
    }

    private void RebuildMenu()
    {
        _notifyIcon.Text = _localization.Get("App.Title");

        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        if (app.Dispatcher.CheckAccess())
            _contextMenu = BuildContextMenu();
        else
            app.Dispatcher.Invoke(() => _contextMenu = BuildContextMenu());
    }

    private ContextMenu BuildContextMenu()
    {
        var app = System.Windows.Application.Current;
        var menuStyle = app?.TryFindResource("StmContextMenu") as Style;
        var separatorStyle = app?.TryFindResource("StmMenuSeparator") as Style;

        var menu = new ContextMenu();
        if (menuStyle is not null)
            menu.Style = menuStyle;

        menu.Items.Add(CreateMenuItem("Tray.Open", (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("Tray.StartAll", async (_, _) =>
            await _tunnelManager.StartAllAsync().ConfigureAwait(false)));
        menu.Items.Add(CreateMenuItem("Tray.StopAll", async (_, _) =>
            await _tunnelManager.StopAllAsync().ConfigureAwait(false)));
        menu.Items.Add(CreateSeparator(separatorStyle));
        menu.Items.Add(CreateMenuItem("Tray.LockVault", (_, _) => _vaultService.Lock(manual: true)));
        menu.Items.Add(CreateSeparator(separatorStyle));
        menu.Items.Add(CreateMenuItem("Tray.Exit", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        return menu;
    }

    private MenuItem CreateMenuItem(string key, RoutedEventHandler onClick)
    {
        var item = new MenuItem { Header = _localization.Get(key) };
        item.Click += onClick;
        return item;
    }

    private static Separator CreateSeparator(Style? style)
    {
        var separator = new Separator();
        if (style is not null)
            separator.Style = style;

        return separator;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
