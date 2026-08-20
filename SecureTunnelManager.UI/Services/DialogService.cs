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

    public async Task<bool> ShowRdpEditorAsync(RdpTarget? target = null)
    {
        var vm = _serviceProvider.GetRequiredService<RdpEditorViewModel>();
        await vm.InitializeAsync(target).ConfigureAwait(true);

        var window = new RdpEditorWindow { DataContext = vm };
        PrepareDialog(window);
        return await ShowModalAsync(vm, window).ConfigureAwait(true);
    }

    public Task<string?> PickRdpGroupAsync(
        string title,
        string message,
        IReadOnlyList<string> existingGroups,
        string? currentGroup,
        bool allowClear = true)
    {
        var localization = GetLocalization();
        var window = new GroupPickerWindow(
            title,
            message,
            localization.Get("Rdp.Group.Label"),
            localization.Get("Rdp.Group.NoGroup"),
            existingGroups,
            currentGroup,
            allowClear)
        {
            CancelButtonContent = localization.Get("Common.Cancel"),
            OkButtonContent = localization.Get("Common.Ok")
        };
        PrepareDialog(window);
        return Task.FromResult(window.ShowDialog() == true ? window.SelectedGroup : null);
    }

    public Task<string?> PromptPasswordAsync(string title, string message)
    {
        var window = new PasswordPromptWindow(title, message);
        PrepareDialog(window);
        var result = window.ShowDialog();
        return Task.FromResult(result == true ? window.Password : null);
    }

    public async Task<ShareImportResult?> ShowShareWizardAsync()
    {
        var vm = _serviceProvider.GetRequiredService<ShareWizardViewModel>();
        await vm.InitializeAsync().ConfigureAwait(true);

        var window = new ShareWizardWindow { DataContext = vm };
        PrepareDialog(window);
        await ShowModalAsync(vm, window).ConfigureAwait(true);
        return vm.DialogResult ? vm.ImportResult : null;
    }

    public void ShowError(string message) =>
        System.Windows.MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) =>
        System.Windows.MessageBox.Show(message, "Secure Tunnel Manager", MessageBoxButton.OK, MessageBoxImage.Information);

    public bool ShowConfirm(string message, string title, bool destructiveConfirm = false)
    {
        var localization = GetLocalization();
        var window = new ConfirmWindow(
            title,
            message,
            localization.Get("Common.Yes"),
            localization.Get("Common.No"),
            useDestructiveConfirm: destructiveConfirm);
        PrepareDialog(window);
        return window.ShowDialog() == true;
    }

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

    private static Task<bool> ShowModalAsync(RdpEditorViewModel vm, RdpEditorWindow window)
        => ShowModalAsync(window, () => vm.DialogResult, h => vm.RequestClose += h);

    private static Task<bool> ShowModalAsync(ShareWizardViewModel vm, ShareWizardWindow window)
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
