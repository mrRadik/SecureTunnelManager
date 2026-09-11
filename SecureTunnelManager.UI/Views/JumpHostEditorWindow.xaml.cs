using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views;

public partial class JumpHostEditorWindow
{
    public JumpHostEditorWindow() => InitializeComponent();

    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is JumpHostEditorViewModel vm)
            vm.Password = PasswordBox.Password;
    }

    private void OnKeyPassphraseChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is JumpHostEditorViewModel vm)
            vm.KeyPassphrase = KeyPassphraseBox.Password;
    }
}
