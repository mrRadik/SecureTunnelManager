using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.UI.Services;

public interface IDialogService
{
    Task<bool> ShowUnlockVaultAsync();
    Task<bool> ShowVaultSetupAsync();
    Task<bool> ShowTunnelEditorAsync(TunnelProfile? profile = null);
    Task<bool> ShowRdpEditorAsync(RdpTarget? target = null);
    Task<string?> PickRdpGroupAsync(
        string title,
        string message,
        IReadOnlyList<string> existingGroups,
        string? currentGroup,
        bool allowClear = true);
    Task<string?> PromptPasswordAsync(string title, string message);
    void ShowError(string message);
    void ShowInfo(string message);
    bool ShowConfirm(string message, string title, bool destructiveConfirm = false);
    void ShowWhatsNew(string version, string releaseNotes);
}
