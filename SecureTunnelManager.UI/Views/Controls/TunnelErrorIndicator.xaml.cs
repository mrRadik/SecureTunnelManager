using System.Windows;
using System.Windows.Controls;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class TunnelErrorIndicator : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ErrorTextProperty =
        DependencyProperty.Register(
            nameof(ErrorText),
            typeof(string),
            typeof(TunnelErrorIndicator),
            new PropertyMetadata(string.Empty, OnErrorTextChanged));

    public static readonly DependencyProperty ShowErrorIndicatorProperty =
        DependencyProperty.Register(
            nameof(ShowErrorIndicator),
            typeof(bool),
            typeof(TunnelErrorIndicator),
            new PropertyMetadata(false));

    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    public bool ShowErrorIndicator
    {
        get => (bool)GetValue(ShowErrorIndicatorProperty);
        set => SetValue(ShowErrorIndicatorProperty, value);
    }

    public TunnelErrorIndicator()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyToolTip(ErrorText);
    }

    private static void OnErrorTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TunnelErrorIndicator indicator)
            indicator.ApplyToolTip(e.NewValue as string);
    }

    private void ApplyToolTip(string? text)
    {
        ToolTipService.SetToolTip(IndicatorBorder, string.IsNullOrWhiteSpace(text) ? null : text);
    }
}
