using System.Windows.Input;
using SecureTunnelManager.UI.Views.Controls;
using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class PasswordPromptWindow : StmChromeWindow
{
    public PasswordPromptWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;
        ContentRendered += (_, _) =>
        {
            PasswordBox.Focus();
            Keyboard.Focus(PasswordBox);
        };
    }

    public string Password => PasswordBox.Password;

    private void OnOkClick(object sender, System.Windows.RoutedEventArgs e) => DialogResult = true;
}
