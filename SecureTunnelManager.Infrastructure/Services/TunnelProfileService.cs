using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
}
