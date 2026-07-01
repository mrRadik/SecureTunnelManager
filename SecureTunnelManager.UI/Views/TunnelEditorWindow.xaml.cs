using SecureTunnelManager.UI.ViewModels;
using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class TunnelEditorWindow : StmChromeWindow
{
    public TunnelEditorWindow() => InitializeComponent();

    private void SyncCredentialsFromView(TunnelEditorViewModel vm)
    {
        vm.TargetPassword = TargetPasswordBox.Password;
        vm.TargetKeyPassphrase = TargetKeyPassphraseBox.Password;
    }

    private void OnNextClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TunnelEditorViewModel vm)
            return;

        SyncCredentialsFromView(vm);
        vm.NextCommand.Execute(null);
    }

    private async void OnSaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TunnelEditorViewModel vm)
            return;

        SyncCredentialsFromView(vm);
        await vm.SaveCommand.ExecuteAsync(null);
    }

    private async void OnTestClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TunnelEditorViewModel vm)
            return;

        SyncCredentialsFromView(vm);
        await vm.TestTunnelCommand.ExecuteAsync(null);
    }

    private void OnTargetPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TunnelEditorViewModel vm)
            vm.TargetCredentialError = string.Empty;
    }
}
