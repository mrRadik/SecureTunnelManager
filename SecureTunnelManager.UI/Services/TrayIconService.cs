using System.IO;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly IVaultService _vaultService;
    private readonly ITunnelManagerService _tunnelManager;
    private readonly ILocalizationService _localization;

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
        _localization.LanguageChanged += (_, _) => RebuildMenu();
        RebuildMenu();
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
    }

    private void TrySetIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
                _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            else
                _notifyIcon.Icon = System.Drawing.SystemIcons.Shield;
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Shield;
        }
    }

    private void RebuildMenu()
    {
        _notifyIcon.Text = _localization.Get("App.Title");

        var menu = new System.Windows.Forms.ContextMenuStrip();

        menu.Items.Add(_localization.Get("Tray.Open"), null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_localization.Get("Tray.StartAll"), null, async (_, _) => await _tunnelManager.StartAllAsync().ConfigureAwait(false));
        menu.Items.Add(_localization.Get("Tray.StopAll"), null, async (_, _) => await _tunnelManager.StopAllAsync().ConfigureAwait(false));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(_localization.Get("Tray.LockVault"), null, (_, _) => _vaultService.Lock());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(_localization.Get("Tray.Exit"), null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon.ContextMenuStrip = menu;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
