using System.Windows;
using System.Windows.Controls;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views;

public partial class RdpEditorWindow
{
    public RdpEditorWindow() => InitializeComponent();

    private void SyncCredentialsFromView(RdpEditorViewModel vm)
    {
        vm.RdpPassword = RdpPasswordBox.Password;
    }

    private void OnRdpPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RdpEditorViewModel vm && sender is PasswordBox box)
        {
            vm.RdpPassword = box.Password;
            vm.RdpCredentialError = string.Empty;
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RdpEditorViewModel vm)
            return;

        SyncCredentialsFromView(vm);
        vm.NextCommand.Execute(null);
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RdpEditorViewModel vm)
            return;

        SyncCredentialsFromView(vm);
        await vm.SaveCommand.ExecuteAsync(null);
    }
}
