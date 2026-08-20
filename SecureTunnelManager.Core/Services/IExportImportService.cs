using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface IExportImportService
{
    Task ExportConnectionsAsync(
        IReadOnlyList<int> tunnelIds,
        IReadOnlyList<int> rdpIds,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<ConnectionShareBundle> ReadBundleFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<ShareImportResult> ImportConnectionsAsync(
        ConnectionShareBundle bundle,
        CancellationToken cancellationToken = default);
}
