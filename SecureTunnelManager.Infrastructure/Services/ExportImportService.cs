using System.Text.Json;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Mapping;
using SecureTunnelManager.Infrastructure.Security;

namespace SecureTunnelManager.Infrastructure.Services;

public class ExportImportService : IExportImportService
{
    private readonly ITunnelProfileService _tunnelProfileService;
    private readonly ILogger<ExportImportService> _logger;

    public ExportImportService(ITunnelProfileService tunnelProfileService, ILogger<ExportImportService> logger)
    {
        _tunnelProfileService = tunnelProfileService;
        _logger = logger;
    }

    public async Task ExportToEncryptedFileAsync(
        IEnumerable<int> profileIds,
        string filePath,
        string exportPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportPassword);

        var ids = profileIds.ToList();
        var exports = new List<TunnelExportDto>();

        foreach (var id in ids)
        {
            var profile = await _tunnelProfileService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Profile {id} not found.");
            exports.Add(EntityMapper.ToExportDto(profile));
        }

        var json = JsonSerializer.Serialize(exports);
        var salt = AesEncryptionService.GenerateSalt();
        var key = AesEncryptionService.DeriveKey(exportPassword, salt);
        var encryptedPayload = AesEncryptionService.Encrypt(json, key);

        var file = new EncryptedExportFile
        {
            Version = 1,
            Salt = Convert.ToBase64String(salt),
            Iv = string.Empty,
            Payload = encryptedPayload
        };

        var fileJson = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, fileJson, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Exported {Count} tunnel profiles to {Path}", exports.Count, filePath);
    }

    public async Task<IReadOnlyList<TunnelProfile>> ImportFromEncryptedFileAsync(
        string filePath,
        string exportPassword,
        CancellationToken cancellationToken = default)
    {
        var fileJson = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var file = JsonSerializer.Deserialize<EncryptedExportFile>(fileJson)
            ?? throw new InvalidOperationException("Invalid .stm file format.");

        var salt = Convert.FromBase64String(file.Salt);
        var key = AesEncryptionService.DeriveKey(exportPassword, salt);
        var json = AesEncryptionService.Decrypt(file.Payload, key);
        var exports = JsonSerializer.Deserialize<List<TunnelExportDto>>(json)
            ?? throw new InvalidOperationException("Invalid export payload.");

        var profiles = exports.Select(EntityMapper.FromExportDto).ToList();
        _logger.LogInformation("Imported {Count} tunnel profiles from {Path}", profiles.Count, filePath);
        return profiles;
    }
}
