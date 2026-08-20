using CommunityToolkit.Mvvm.ComponentModel;

namespace SecureTunnelManager.UI.ViewModels;

public enum ShareConnectionKind
{
    Tunnel,
    Rdp
}

public sealed partial class ShareConnectionItemViewModel : ObservableObject
{
    public ShareConnectionKind Kind { get; init; }

    public int ResourceId { get; init; }

    /// <summary>Index in the import bundle list (tunnels or RDP).</summary>
    public int BundleIndex { get; init; }

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _subtitle = string.Empty;

    [ObservableProperty] private bool _isSelected = true;

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    partial void OnSubtitleChanged(string value) => OnPropertyChanged(nameof(HasSubtitle));
}
