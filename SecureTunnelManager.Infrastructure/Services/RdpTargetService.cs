using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

public class RdpTargetService : IRdpTargetService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<RdpTargetService> _logger;

    public RdpTargetService(IDbContextFactory<AppDbContext> dbFactory, ILogger<RdpTargetService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RdpTarget>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.RdpTargets.AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(EntityMapper.ToModel).ToList();
    }

    public async Task<RdpTarget?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.RdpTargets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : EntityMapper.ToModel(entity);
    }

    public async Task<int> CreateAsync(RdpTarget target, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = EntityMapper.ToEntity(target);
        entity.CreatedDate = DateTime.UtcNow;
        entity.ModifiedDate = DateTime.UtcNow;
        db.RdpTargets.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("RDP target created: {Name}", target.Name);
        return entity.Id;
    }

    public async Task UpdateAsync(RdpTarget target, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.RdpTargets.FirstOrDefaultAsync(t => t.Id == target.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"RDP target {target.Id} not found.");

        EntityMapper.UpdateEntity(entity, target);
        entity.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("RDP target updated: {Name}", target.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.RdpTargets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return;

        db.RdpTargets.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("RDP target deleted: {Name}", entity.Name);
    }
}
