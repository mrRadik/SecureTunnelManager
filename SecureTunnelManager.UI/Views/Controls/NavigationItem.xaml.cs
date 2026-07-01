using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Localization;
using SecureTunnelManager.UI.Services;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class NavigationItem : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(NavigationItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LocKeyProperty =
        DependencyProperty.Register(nameof(LocKey), typeof(string), typeof(NavigationItem),
            new PropertyMetadata(string.Empty, OnLocKeyPropertyChanged));

    public static readonly DependencyProperty SectionProperty =
        DependencyProperty.Register(nameof(Section), typeof(NavigationSection), typeof(NavigationItem), new PropertyMetadata(NavigationSection.Tunnels));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(NavigationItem), new PropertyMetadata(null));

    private ILocalizationService? _localization;

    public NavigationItem()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string LocKey
    {
        get => (string)GetValue(LocKeyProperty);
        set => SetValue(LocKeyProperty, value);
    }

    public NavigationSection Section
    {
        get => (NavigationSection)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private static void OnLocKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationItem item)
            item.BindLabelText();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App)
            return;

        _localization = App.Services.GetRequiredService<ILocalizationService>();
        _localization.LanguageChanged += OnLanguageChanged;
        BindLabelText();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_localization is not null)
            _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => BindLabelText();

    private void BindLabelText()
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
        LabelText.SetBinding(TextBlock.TextProperty, binding);
    }
}
