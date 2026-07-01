using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI;

public partial class MainWindow : StmChromeWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel { Settings.CloseToTray: true })
        {
            e.Cancel = true;
            Hide();
            return;
        }

        e.Cancel = false;
        if (System.Windows.Application.Current is App app)
            app.ShutdownApplication();
    }
}
