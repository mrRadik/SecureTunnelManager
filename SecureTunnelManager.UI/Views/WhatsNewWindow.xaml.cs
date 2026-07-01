using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class WhatsNewWindow : StmChromeWindow
{
    public WhatsNewWindow(string version, string releaseNotes, string title, string subtitle, string okText)
    {
        InitializeComponent();
        Title = title;
        TitleBar.Title = title;
        SubtitleText.Text = subtitle;
        ReleaseNotesText.Text = releaseNotes;
        OkButton.Content = okText;
    }

    private void OnOkClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
