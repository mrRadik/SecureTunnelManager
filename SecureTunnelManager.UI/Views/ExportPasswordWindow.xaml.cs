using SecureTunnelManager.UI.Views.Controls;
using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class ExportPasswordWindow : StmChromeWindow
{
    public ExportPasswordWindow(bool isExport)
    {
        InitializeComponent();
        HintText.Text = isExport
            ? "Enter a password to encrypt the export file (.stm). You will need this password to import."
            : "Enter the password used when exporting this .stm file.";
        Title = isExport ? "Export Password" : "Import Password";
        TitleBar.Title = Title;
    }

    public string Password => PasswordBox.Password;

    private void OnOkClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = !string.IsNullOrWhiteSpace(PasswordBox.Password);
    }
}
