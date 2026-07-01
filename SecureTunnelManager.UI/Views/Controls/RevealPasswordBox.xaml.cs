using System.Windows;
using System.Windows.Controls;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class RevealPasswordBox : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(RevealPasswordBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

    public static readonly DependencyProperty IsRevealedProperty =
        DependencyProperty.Register(nameof(IsRevealed), typeof(bool), typeof(RevealPasswordBox), new PropertyMetadata(false));

    private bool _isSyncing;

    public RevealPasswordBox() => InitializeComponent();

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RevealPasswordBox box)
            box.SyncFromProperty();
    }

    private void SyncFromProperty()
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            if (HiddenBox.Password != Password)
                HiddenBox.Password = Password ?? string.Empty;
            if (VisibleBox.Text != Password)
                VisibleBox.Text = Password ?? string.Empty;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncing || IsRevealed) return;
        _isSyncing = true;
        try
        {
            Password = HiddenBox.Password;
            VisibleBox.Text = HiddenBox.Password;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing || !IsRevealed) return;
        _isSyncing = true;
        try
        {
            Password = VisibleBox.Text;
            HiddenBox.Password = VisibleBox.Text;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnToggleReveal(object sender, RoutedEventArgs e)
    {
        IsRevealed = !IsRevealed;
        RevealGlyph.Text = IsRevealed ? "\uED1B" : "\uED1A";
        SyncFromProperty();
    }
}
