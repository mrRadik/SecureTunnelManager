using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Infrastructure;
using SecureTunnelManager.UI.Services;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private TrayIconService? _tray;
    private SingleInstanceManager? _singleInstance;
    private bool _isExiting;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("Application host is not initialized.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new SingleInstanceManager();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.RequestActivation();
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureTunnelManager");

        Directory.CreateDirectory(appData);

        var dbPath = Path.Combine(appData, "secure-tunnel-manager.db");
        var logDir = Path.Combine(appData, "logs");

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(services =>
            {
                services.AddSecureTunnelInfrastructure(dbPath, logDir);
                services.AddSingleton<ILocalizationService, LocalizationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<UpdatePromptService>();
                services.AddSingleton<WhatsNewService>();
                services.AddSingleton<TrayIconService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddTransient<VaultSetupViewModel>();
                services.AddTransient<UnlockVaultViewModel>();
                services.AddTransient<TunnelEditorViewModel>();
            })
            .Build();

        await _host.StartAsync().ConfigureAwait(true);
        await DatabaseInitializer.InitializeAsync(_host.Services).ConfigureAwait(true);

        var settingsService = Services.GetRequiredService<ISettingsService>();
        var localization = Services.GetRequiredService<ILocalizationService>();
        var appSettings = await settingsService.GetSettingsAsync().ConfigureAwait(true);
        localization.ApplyLanguage(appSettings.UiLanguage);
        Resources["Loc"] = localization;

        // Create main window early so modal dialogs can use it as Owner after first show.
        var main = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>(),
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        MainWindow = main;

        var vault = Services.GetRequiredService<IVaultService>();
        var dialog = Services.GetRequiredService<IDialogService>();

        if (!await vault.IsVaultInitializedAsync().ConfigureAwait(true))
        {
            if (!await dialog.ShowVaultSetupAsync().ConfigureAwait(true))
            {
                ShutdownApplication();
                return;
            }
        }
        else if (!await dialog.ShowUnlockVaultAsync().ConfigureAwait(true))
        {
            // Allow tray-only mode; user can unlock later
        }

        _tray = Services.GetRequiredService<TrayIconService>();
        _tray.OpenRequested += (_, _) => ShowMainWindow();
        _tray.ExitRequested += (_, _) => ShutdownApplication();

        main.Show();

        if (MainWindow.DataContext is MainViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);

        _singleInstance.StartActivationListener(ShowMainWindow);

        var whatsNew = Services.GetRequiredService<WhatsNewService>();
        await whatsNew.TryShowWhatsNewAsync().ConfigureAwait(true);

        if (appSettings.CheckForUpdatesOnStartup)
            _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
            var updatePrompt = Services.GetRequiredService<UpdatePromptService>();
            await updatePrompt.CheckAndPromptAsync(silentWhenUpToDate: true).ConfigureAwait(true);
        }
        catch
        {
            // Startup update check must not block or crash the app.
        }
    }

    public void ShowMainWindow()
    {
        if (MainWindow is null) return;
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    public void ShutdownApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            var tunnelManager = Services.GetRequiredService<ITunnelManagerService>();
            await tunnelManager.StopAllAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore shutdown errors
        }

        _tray?.Dispose();
        _singleInstance?.Dispose();
        _singleInstance = null;

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
