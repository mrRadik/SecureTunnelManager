using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Localization;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class WindowTitleBar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(WindowTitleBar),
            new PropertyMetadata(string.Empty, OnTitlePropertyChanged));

    public static readonly DependencyProperty LocKeyProperty =
        DependencyProperty.Register(nameof(LocKey), typeof(string), typeof(WindowTitleBar),
            new PropertyMetadata(string.Empty, OnTitlePropertyChanged));

    public static readonly DependencyProperty ShowMaximizeButtonProperty =
        DependencyProperty.Register(nameof(ShowMaximizeButton), typeof(bool), typeof(WindowTitleBar), new PropertyMetadata(true));

    private Window? _window;
    private ILocalizationService? _localization;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string LocKey
    {
        get => (string)GetValue(LocKeyProperty);
        set => SetValue(LocKeyProperty, value);
    }

    public bool ShowMaximizeButton
    {
        get => (bool)GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    public WindowTitleBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnTitlePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WindowTitleBar bar)
            bar.UpdateTitleText();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
            _window.StateChanged += OnWindowStateChanged;

        if (System.Windows.Application.Current is App)
        {
            _localization = App.Services.GetRequiredService<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
            BindTitleText();
        }

        UpdateMaximizeGlyph();
        UpdateTitleText();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
            _window.StateChanged -= OnWindowStateChanged;

        if (_localization is not null)
            _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e) => UpdateMaximizeGlyph();

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateTitleText();

    private void BindTitleText()
    {
        if (_localization is null || string.IsNullOrWhiteSpace(LocKey))
            return;

        var binding = new MultiBinding
        {
            Converter = LocKeyConverter.Instance,
            Mode = BindingMode.OneWay
        };
        binding.Bindings.Add(new System.Windows.Data.Binding(nameof(ILocalizationService.Version)) { Source = _localization });
        binding.Bindings.Add(new System.Windows.Data.Binding(nameof(LocKey)) { Source = this });
        TitleText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, binding);
    }

    private void UpdateTitleText()
    {
        if (!string.IsNullOrWhiteSpace(LocKey) && _localization is not null)
        {
            TitleText.Text = _localization.Get(LocKey);
            return;
        }

        TitleText.Text = Title;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_window is null)
            return;

        if (e.ClickCount == 2 && _window.ResizeMode == ResizeMode.CanResize)
        {
            ToggleMaximize();
            return;
        }

        if (e.ClickCount == 1)
            NativeWindowHelper.DragWindow(_window);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
            _window.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
            _window.Close();
    }

    private void ToggleMaximize()
    {
        if (_window is null || _window.ResizeMode != ResizeMode.CanResize)
            return;

        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaximizeGlyph();
    }

    private void UpdateMaximizeGlyph()
    {
        if (_window is null || MaximizeGlyph is null)
            return;

        MaximizeGlyph.Text = _window.WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }
}
