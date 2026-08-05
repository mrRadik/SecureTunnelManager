using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class RdpStatusBadge : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status),
            typeof(RdpSessionStatus),
            typeof(RdpStatusBadge),
            new PropertyMetadata(RdpSessionStatus.Disconnected, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsSubtleProperty =
        DependencyProperty.Register(
            nameof(IsSubtle),
            typeof(bool),
            typeof(RdpStatusBadge),
            new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(RdpStatusBadge),
            new PropertyMetadata(false, OnVisualPropertyChanged));

    private Storyboard? _pulseStoryboard;
    private ILocalizationService? _localization;

    public RdpStatusBadge()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public RdpSessionStatus Status
    {
        get => (RdpSessionStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public bool IsSubtle
    {
        get => (bool)GetValue(IsSubtleProperty);
        set => SetValue(IsSubtleProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RdpStatusBadge badge)
            badge.ApplyVisualState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App)
        {
            _localization = App.Services.GetRequiredService<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
        }

        ApplyVisualState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_localization is not null)
            _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyVisualState();

    private void ApplyVisualState()
    {
        StopPulse();

        switch (Status)
        {
            case RdpSessionStatus.Connected:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusConnectedBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusConnectedDotBrush"),
                    Localize("Status.Connected"));
                break;
            case RdpSessionStatus.Connecting:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusReconnectingBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusReconnectingDotBrush"),
                    Localize("Status.Connecting"));
                StartPulse();
                break;
            case RdpSessionStatus.Error:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusErrorBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusErrorDotBrush"),
                    Localize("Status.Error"));
                break;
            default:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusStoppedBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusStoppedDotBrush"),
                    Localize("Status.Disconnected"));
                break;
        }
    }

    private string Localize(string key) => _localization?.Get(key) ?? key;

    private void SetBadge(System.Windows.Media.Brush background, System.Windows.Media.Brush dot, string label)
    {
        if (BadgeBorder is null || StatusDot is null || StatusLabel is null)
            return;

        if (IsCompact)
        {
            BadgeBorder.Background = System.Windows.Media.Brushes.Transparent;
            BadgeBorder.Padding = new Thickness(0);
            BadgeBorder.Opacity = 1;
            StatusDot.Width = 7;
            StatusDot.Height = 7;
            StatusDot.Margin = new Thickness(0);
            StatusDot.Fill = dot;
            StatusDot.Opacity = 1;
            StatusLabel.Visibility = Visibility.Collapsed;
            ToolTip = label;
            return;
        }

        ToolTip = null;
        StatusLabel.Visibility = Visibility.Visible;

        BadgeBorder.Background = background;
        BadgeBorder.Opacity = IsSubtle ? 0.72 : 1;
        BadgeBorder.Padding = IsSubtle ? new Thickness(6, 3, 6, 3) : new Thickness(8, 4, 8, 4);
        StatusDot.Width = IsSubtle ? 6 : 8;
        StatusDot.Height = IsSubtle ? 6 : 8;
        StatusDot.Margin = new Thickness(0, 0, 6, 0);
        StatusDot.Fill = dot;
        StatusDot.Opacity = IsSubtle ? 0.85 : 1;
        StatusLabel.Text = label;

        if (IsSubtle)
        {
            StatusLabel.FontSize = 11;
            StatusLabel.FontWeight = System.Windows.FontWeights.Normal;
            StatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("StmTextSecondaryBrush");
            StatusLabel.Opacity = 0.88;
            return;
        }

        StatusLabel.ClearValue(FontSizeProperty);
        StatusLabel.ClearValue(FontWeightProperty);
        StatusLabel.ClearValue(ForegroundProperty);
        StatusLabel.ClearValue(System.Windows.Controls.TextBlock.OpacityProperty);
    }

    private void StartPulse()
    {
        _pulseStoryboard = (Storyboard)FindResource("PulseStoryboard");
        _pulseStoryboard.Begin(this, true);
    }

    private void StopPulse()
    {
        if (_pulseStoryboard is not null)
        {
            _pulseStoryboard.Stop(this);
            _pulseStoryboard = null;
        }

        StatusDot.Opacity = 1;
    }
}
