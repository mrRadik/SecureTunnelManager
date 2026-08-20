using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class RevealPasswordBox : System.Windows.Controls.UserControl
{
    private const int SavedPasswordMaskLength = 8;

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(RevealPasswordBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

    public static readonly DependencyProperty IsRevealedProperty =
        DependencyProperty.Register(nameof(IsRevealed), typeof(bool), typeof(RevealPasswordBox), new PropertyMetadata(false));

    public static readonly DependencyProperty ShowSavedPasswordMaskProperty =
        DependencyProperty.Register(
            nameof(ShowSavedPasswordMask),
            typeof(bool),
            typeof(RevealPasswordBox),
            new PropertyMetadata(false, OnShowSavedPasswordMaskChanged));

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

    public bool ShowSavedPasswordMask
    {
        get => (bool)GetValue(ShowSavedPasswordMaskProperty);
        set => SetValue(ShowSavedPasswordMaskProperty, value);
    }

    private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RevealPasswordBox box)
            box.SyncFromProperty();
    }

    private static void OnShowSavedPasswordMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RevealPasswordBox box)
            box.ApplySavedPasswordMask();
    }

    private void SyncFromProperty()
    {
        if (_isSyncing || ShowSavedPasswordMask)
            return;

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

    private void ApplySavedPasswordMask()
    {
        if (_isSyncing)
            return;

        _isSyncing = true;
        try
        {
            if (ShowSavedPasswordMask)
            {
                var mask = new string('\u2022', SavedPasswordMaskLength);
                HiddenBox.Password = new string('0', SavedPasswordMaskLength);
                VisibleBox.Text = mask;
                return;
            }

            if (string.IsNullOrEmpty(Password))
            {
                HiddenBox.Password = string.Empty;
                VisibleBox.Text = string.Empty;
            }
            else
            {
                SyncFromProperty();
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncing || IsRevealed)
            return;

        if (ShowSavedPasswordMask)
            SetCurrentValue(ShowSavedPasswordMaskProperty, false);

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
        if (_isSyncing || !IsRevealed)
            return;

        if (ShowSavedPasswordMask)
            SetCurrentValue(ShowSavedPasswordMaskProperty, false);

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

        if (ShowSavedPasswordMask)
            ApplySavedPasswordMask();
        else
            SyncFromProperty();
    }

    public void FocusInput()
    {
        Dispatcher.BeginInvoke(() =>
        {
            var target = (UIElement)(IsRevealed ? VisibleBox : HiddenBox);
            target.Focus();
            Keyboard.Focus(target);
        }, DispatcherPriority.Input);
    }
}
