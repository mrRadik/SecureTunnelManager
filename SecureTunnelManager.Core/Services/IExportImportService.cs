using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core.Services;

public interface IExportImportService
{
    Task ExportToEncryptedFileAsync(IEnumerable<int> profileIds, string filePath, string exportPassword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TunnelProfile>> ImportFromEncryptedFileAsync(string filePath, string exportPassword, CancellationToken cancellationToken = default);
}
