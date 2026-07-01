using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.ViewModels;
using SecureTunnelManager.UI.Views;

namespace SecureTunnelManager.UI.Services;

public class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public DialogService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<bool> ShowVaultSetupAsync()
    {
        var vm = _serviceProvider.GetRequiredService<VaultSetupViewModel>();
        var window = new VaultSetupWindow { DataContext = vm };
        PrepareDialog(window);
        return ShowModalAsync(vm, window);
    }

    public Task<bool> ShowUnlockVaultAsync()
    {
        var vm = _serviceProvider.GetRequiredService<UnlockVaultViewModel>();
        var window = new UnlockVaultWindow { DataContext = vm };
        PrepareDialog(window);
        return ShowModalAsync(vm, window);
    }

    public async Task<bool> ShowTunnelEditorAsync(TunnelProfile? profile = null)
    {
        var vm = _serviceProvider.GetRequiredService<TunnelEditorViewModel>();
        await vm.InitializeAsync(profile).ConfigureAwait(true);

        var window = new TunnelEditorWindow { DataContext = vm };
        PrepareDialog(window);
        return await ShowModalAsync(vm, window).ConfigureAwait(true);
    }

    public Task<string?> PromptPasswordAsync(string title, string message)
    {
        var window = new PasswordPromptWindow(title, message);
        PrepareDialog(window);
        var result = window.ShowDialog();
        return Task.FromResult(result == true ? window.Password : null);
    }

    public Task<(string Path, string Password)?> PromptExportAsync(IReadOnlyList<TunnelListItemViewModel> selected)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Secure Tunnel Manager (*.stm)|*.stm",
            FileName = "tunnels.stm"
        };

        if (dialog.ShowDialog() != true)
            return Task.FromResult<(string, string)?>(null);

        var passwordWindow = new ExportPasswordWindow(isExport: true);
        PrepareDialog(passwordWindow);
        if (passwordWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(passwordWindow.Password))
            return Task.FromResult<(string, string)?>(null);

        return Task.FromResult<(string, string)?>((dialog.FileName, passwordWindow.Password));
    }

    public Task<(string Path, string Password)?> PromptImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Secure Tunnel Manager (*.stm)|*.stm"
        };

        if (dialog.ShowDialog() != true)
            return Task.FromResult<(string, string)?>(null);

        var passwordWindow = new ExportPasswordWindow(isExport: false);
        PrepareDialog(passwordWindow);
        if (passwordWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(passwordWindow.Password))
            return Task.FromResult<(string, string)?>(null);

        return Task.FromResult<(string, string)?>((dialog.FileName, passwordWindow.Password));
    }

    public void ShowError(string message) =>
        System.Windows.MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) =>
        System.Windows.MessageBox.Show(message, "Secure Tunnel Manager", MessageBoxButton.OK, MessageBoxImage.Information);

    public bool ShowConfirm(string message, string title) =>
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowWhatsNew(string version, string releaseNotes)
    {
        var localization = GetLocalization();
        var window = new WhatsNewWindow(
            version,
            releaseNotes,
            localization.Get("WhatsNew.Title"),
            localization.Format("WhatsNew.Subtitle", version),
            localization.Get("WhatsNew.Ok"));
        PrepareDialog(window);
        window.ShowDialog();
    }

    private static ILocalizationService GetLocalization()
    {
        if (System.Windows.Application.Current?.Resources["Loc"] is ILocalizationService localization)
            return localization;

        return new LocalizationService();
    }

    private static Task<bool> ShowModalAsync(VaultSetupViewModel vm, VaultSetupWindow window)
        => ShowModalAsync(window, () => vm.DialogResult, h => vm.RequestClose += h);

    private static Task<bool> ShowModalAsync(UnlockVaultViewModel vm, UnlockVaultWindow window)
        => ShowModalAsync(window, () => vm.DialogResult, h => vm.RequestClose += h);

    private static Task<bool> ShowModalAsync(TunnelEditorViewModel vm, TunnelEditorWindow window)
        => ShowModalAsync(window, () => vm.DialogResult, h => vm.RequestClose += h);

    private static Task<bool> ShowModalAsync(
        Window window,
        Func<bool> getResult,
        Action<EventHandler> subscribeClose)
    {
        var tcs = new TaskCompletionSource<bool>();

        subscribeClose((_, _) =>
        {
            window.DialogResult = getResult();
            window.Close();
        });

        window.Closed += (_, _) => tcs.TrySetResult(getResult());
        window.ShowDialog();
        return tcs.Task;
    }

    /// <summary>
    /// WPF requires the owner window to have been shown at least once before assigning Owner.
    /// </summary>
    private static void PrepareDialog(Window dialog)
    {
        var owner = GetValidOwner(dialog);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private static Window? GetValidOwner(Window dialog)
    {
        var owner = System.Windows.Application.Current.MainWindow;

        if (owner is null || ReferenceEquals(owner, dialog))
            return null;

        // Owner must have been shown at least once (IsLoaded == true after Show/ShowDialog)
        if (!owner.IsLoaded)
            return null;

        return owner;
    }
}
