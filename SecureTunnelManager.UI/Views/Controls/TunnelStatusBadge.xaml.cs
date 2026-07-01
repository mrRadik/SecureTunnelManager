using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class TunnelStatusBadge : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status),
            typeof(TunnelStatus),
            typeof(TunnelStatusBadge),
            new PropertyMetadata(TunnelStatus.Stopped, OnVisualPropertyChanged));

    public static readonly DependencyProperty ReconnectAttemptProperty =
        DependencyProperty.Register(
            nameof(ReconnectAttempt),
            typeof(int),
            typeof(TunnelStatusBadge),
            new PropertyMetadata(0, OnVisualPropertyChanged));

    private Storyboard? _pulseStoryboard;
    private ILocalizationService? _localization;

    public TunnelStatusBadge()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

    public TunnelStatus Status
    {
        get => (TunnelStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public int ReconnectAttempt
    {
        get => (int)GetValue(ReconnectAttemptProperty);
        set => SetValue(ReconnectAttemptProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TunnelStatusBadge badge)
            badge.ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        StopPulse();

        switch (Status)
        {
            case TunnelStatus.Connected:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusConnectedBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusConnectedDotBrush"),
                    Localize("Status.Connected"));
                break;
            case TunnelStatus.Connecting:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusReconnectingBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusReconnectingDotBrush"),
                    ReconnectAttempt > 0 ? Localize("Status.Reconnecting") : Localize("Status.Connecting"));
                StartPulse();
                break;
            case TunnelStatus.Error:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusErrorBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusErrorDotBrush"),
                    Localize("Status.Error"));
                break;
            default:
                SetBadge(
                    (System.Windows.Media.Brush)FindResource("StmStatusStoppedBgBrush"),
                    (System.Windows.Media.Brush)FindResource("StmStatusStoppedDotBrush"),
                    Localize("Status.Stopped"));
                break;
        }
    }

    private string Localize(string key) => _localization?.Get(key) ?? key;

    private void SetBadge(System.Windows.Media.Brush background, System.Windows.Media.Brush dot, string label)
    {
        BadgeBorder.Background = background;
        StatusDot.Fill = dot;
        StatusDot.Opacity = 1;
        StatusLabel.Text = label;
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
