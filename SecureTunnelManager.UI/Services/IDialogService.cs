using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.UI.Services;

public interface IDialogService
{
    Task<bool> ShowUnlockVaultAsync();
    Task<bool> ShowVaultSetupAsync();
    Task<bool> ShowTunnelEditorAsync(TunnelProfile? profile = null);
    Task<string?> PromptPasswordAsync(string title, string message);
    Task<(string Path, string Password)?> PromptExportAsync(IReadOnlyList<TunnelListItemViewModel> selected);
    Task<(string Path, string Password)?> PromptImportAsync();
    void ShowError(string message);
    void ShowInfo(string message);
}

/// <summary>
/// Lightweight view model reference for export dialog (defined in ViewModels but referenced here to avoid circular deps).
/// </summary>
public class TunnelListItemViewModel
{
    public int ProfileId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}
