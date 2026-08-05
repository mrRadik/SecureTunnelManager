using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class AppStatusBar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty VaultStatusTextProperty =
        DependencyProperty.Register(nameof(VaultStatusText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsVaultUnlockedProperty =
        DependencyProperty.Register(nameof(IsVaultUnlocked), typeof(bool), typeof(AppStatusBar), new PropertyMetadata(false));

    public static readonly DependencyProperty ActiveConnectionsTextProperty =
        DependencyProperty.Register(nameof(ActiveConnectionsText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionTextProperty =
        DependencyProperty.Register(nameof(VersionText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VaultActionTextProperty =
        DependencyProperty.Register(nameof(VaultActionText), typeof(string), typeof(AppStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VaultActionCommandProperty =
        DependencyProperty.Register(nameof(VaultActionCommand), typeof(ICommand), typeof(AppStatusBar), new PropertyMetadata(null));

    public static readonly DependencyProperty VaultClickCommandProperty =
        DependencyProperty.Register(nameof(VaultClickCommand), typeof(ICommand), typeof(AppStatusBar), new PropertyMetadata(null));

    public AppStatusBar() => InitializeComponent();

    public string VaultStatusText
    {
        get => (string)GetValue(VaultStatusTextProperty);
        set => SetValue(VaultStatusTextProperty, value);
    }

    public bool IsVaultUnlocked
    {
        get => (bool)GetValue(IsVaultUnlockedProperty);
        set => SetValue(IsVaultUnlockedProperty, value);
    }

    public string ActiveConnectionsText
    {
        get => (string)GetValue(ActiveConnectionsTextProperty);
        set => SetValue(ActiveConnectionsTextProperty, value);
    }

    public string VersionText
    {
        get => (string)GetValue(VersionTextProperty);
        set => SetValue(VersionTextProperty, value);
    }

    public string VaultActionText
    {
        get => (string)GetValue(VaultActionTextProperty);
        set => SetValue(VaultActionTextProperty, value);
    }

    public ICommand? VaultActionCommand
    {
        get => (ICommand?)GetValue(VaultActionCommandProperty);
        set => SetValue(VaultActionCommandProperty, value);
    }

    public ICommand? VaultClickCommand
    {
        get => (ICommand?)GetValue(VaultClickCommandProperty);
        set => SetValue(VaultClickCommandProperty, value);
    }

    private void OnVaultStatusClick(object sender, MouseButtonEventArgs e)
    {
        if (VaultClickCommand?.CanExecute(null) == true)
            VaultClickCommand.Execute(null);
    }
}
