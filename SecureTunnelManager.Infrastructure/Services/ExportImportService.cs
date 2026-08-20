using System.Text.Json;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Mapping;
using SecureTunnelManager.Infrastructure.Security;

namespace SecureTunnelManager.Infrastructure.Services;

public class ExportImportService : IExportImportService
{
    private readonly ITunnelProfileService _tunnelProfileService;
    private readonly IRdpTargetService _rdpTargetService;
    private readonly ILogger<ExportImportService> _logger;

    public ExportImportService(
        ITunnelProfileService tunnelProfileService,
        IRdpTargetService rdpTargetService,
        ILogger<ExportImportService> logger)
    {
        _tunnelProfileService = tunnelProfileService;
        _rdpTargetService = rdpTargetService;
        _logger = logger;
    }

    public async Task ExportConnectionsAsync(
        IReadOnlyList<int> tunnelIds,
        IReadOnlyList<int> rdpIds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var bundle = new ConnectionShareBundle();

        foreach (var id in tunnelIds)
        {
            var profile = await _tunnelProfileService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Tunnel profile {id} not found.");
            bundle.Tunnels.Add(EntityMapper.ToExportDto(profile));
        }

        foreach (var id in rdpIds)
        {
            var target = await _rdpTargetService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"RDP target {id} not found.");
            bundle.RdpTargets.Add(EntityMapper.ToExportDto(target));
        }

        if (bundle.Tunnels.Count == 0 && bundle.RdpTargets.Count == 0)
            throw new InvalidOperationException("Nothing selected to export.");

        var json = JsonSerializer.Serialize(bundle);
        var encryptedPayload = ShareFileCrypto.EncryptJson(json);

        var file = new EncryptedExportFile
        {
            Version = 2,
            Payload = encryptedPayload
        };

        var fileJson = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, fileJson, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Exported {TunnelCount} tunnel(s) and {RdpCount} RDP target(s) to {Path}",
            bundle.Tunnels.Count,
            bundle.RdpTargets.Count,
            filePath);
    }

    public async Task<ConnectionShareBundle> ReadBundleFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var fileJson = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var file = JsonSerializer.Deserialize<EncryptedExportFile>(fileJson)
            ?? throw new InvalidOperationException("Invalid .stm file format.");

        if (file.Version != 2)
            throw new InvalidOperationException("Unsupported share file version.");

        var json = ShareFileCrypto.DecryptJson(file.Payload);
        var bundle = JsonSerializer.Deserialize<ConnectionShareBundle>(json)
            ?? throw new InvalidOperationException("Invalid share file payload.");

        return bundle;
    }

    public async Task<ShareImportResult> ImportConnectionsAsync(
        ConnectionShareBundle bundle,
        CancellationToken cancellationToken = default)
    {
        var existingTunnels = await _tunnelProfileService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var existingRdp = await _rdpTargetService.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var tunnelNames = existingTunnels.Select(p => p.Name).ToList();
        var rdpNames = existingRdp.Select(t => t.Name).ToList();
        var tunnelPortPool = existingTunnels.ToList();
        var rdpPortPool = existingRdp.ToList();

        var tunnelsImported = 0;
        foreach (var dto in bundle.Tunnels)
        {
            var profile = EntityMapper.FromExportDto(dto);
            profile.Name = tunnelNames.Contains(dto.Name, StringComparer.OrdinalIgnoreCase)
                ? ResourceCloneHelper.GenerateCopyName(dto.Name, tunnelNames)
                : dto.Name;
            tunnelNames.Add(profile.Name);
            profile.LocalPort = ResourceCloneHelper.ResolveTunnelLocalPort(
                profile.LocalPort,
                profile.LocalBindAddress,
                tunnelPortPool);
            tunnelPortPool.Add(profile);

            await _tunnelProfileService.CreateAsync(profile, cancellationToken).ConfigureAwait(false);
            tunnelsImported++;
        }

        var rdpImported = 0;
        foreach (var dto in bundle.RdpTargets)
        {
            var target = EntityMapper.FromExportDto(dto);
            target.Name = rdpNames.Contains(dto.Name, StringComparer.OrdinalIgnoreCase)
                ? ResourceCloneHelper.GenerateCopyName(dto.Name, rdpNames)
                : dto.Name;
            rdpNames.Add(target.Name);
            target.LocalPort = ResourceCloneHelper.ResolveRdpLocalPort(
                target.LocalPort,
                target.LocalBindAddress,
                rdpPortPool);
            rdpPortPool.Add(target);

            await _rdpTargetService.CreateAsync(target, cancellationToken).ConfigureAwait(false);
            rdpImported++;
        }

        _logger.LogInformation(
            "Imported {TunnelCount} tunnel(s) and {RdpCount} RDP target(s)",
            tunnelsImported,
            rdpImported);

        return new ShareImportResult
        {
            TunnelsImported = tunnelsImported,
            RdpImported = rdpImported
        };
    }
}
