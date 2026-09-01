using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Data.Entities;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

public class TunnelProfileService : ITunnelProfileService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<TunnelProfileService> _logger;

    public TunnelProfileService(IDbContextFactory<AppDbContext> dbFactory, ILogger<TunnelProfileService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TunnelProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.TunnelProfiles.AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(EntityMapper.ToModel).ToList();
    }

    public async Task<TunnelProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.TunnelProfiles.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : EntityMapper.ToModel(entity);
    }

    public async Task<int> CreateAsync(TunnelProfile profile, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = EntityMapper.ToEntity(profile);
        entity.CreatedDate = DateTime.UtcNow;
        entity.ModifiedDate = DateTime.UtcNow;
        db.TunnelProfiles.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Tunnel profile created: {Name}", profile.Name);
        return entity.Id;
    }

    public async Task UpdateAsync(TunnelProfile profile, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.TunnelProfiles.FirstOrDefaultAsync(t => t.Id == profile.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tunnel profile {profile.Id} not found.");

        EntityMapper.UpdateEntity(entity, profile);
        entity.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Tunnel profile updated: {Name}", profile.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.TunnelProfiles.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return;

        var candidateCredentialIds = CredentialReferenceHelper.CollectFromTunnel(EntityMapper.ToModel(entity));
        await OrphanCredentialCleanup.RemoveUnreferencedAsync(
            db,
            candidateCredentialIds,
            excludeTunnelId: id,
            excludeRdpId: null,
            cancellationToken).ConfigureAwait(false);

        db.TunnelProfiles.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Tunnel profile deleted: {Name}", entity.Name);
    }

    public async Task<IReadOnlyList<TunnelProfile>> GetAutoStartProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.TunnelProfiles.AsNoTracking()
            .Where(t => t.StartWithWindows)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(EntityMapper.ToModel).ToList();
    }

    public async Task<IReadOnlyList<string>> GetGroupNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.TunnelProfiles.AsNoTracking()
            .Where(t => t.GroupName != null && t.GroupName != "")
            .Select(t => t.GroupName!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetGroupNameAsync(int profileId, string? groupName, CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.TunnelProfiles.FirstOrDefaultAsync(t => t.Id == profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tunnel profile {profileId} not found.");

        entity.GroupName = normalized;
        entity.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameGroupAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        var normalizedOld = oldName.Trim();
        var normalizedNew = string.IsNullOrWhiteSpace(newName) ? null : newName.Trim();
        if (normalizedNew is not null
            && string.Equals(normalizedOld, normalizedNew, StringComparison.OrdinalIgnoreCase))
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.TunnelProfiles
            .Where(t => t.GroupName == normalizedOld)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entity in entities)
        {
            entity.GroupName = normalizedNew;
            entity.ModifiedDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Tunnel group renamed: {OldName} -> {NewName}", oldName, normalizedNew ?? "(none)");
    }
}
