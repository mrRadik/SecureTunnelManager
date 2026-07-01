using Microsoft.EntityFrameworkCore;
using SecureTunnelManager.Data.Entities;

namespace SecureTunnelManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TunnelProfileEntity> TunnelProfiles => Set<TunnelProfileEntity>();
    public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TunnelProfileEntity>(entity =>
        {
            entity.ToTable("TunnelProfiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.JumpHost).HasMaxLength(255).IsRequired();
            entity.Property(e => e.JumpUsername).HasMaxLength(128).IsRequired();
            entity.Property(e => e.TargetHost).HasMaxLength(255).IsRequired();
            entity.Property(e => e.TargetUsername).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RemoteHost).HasMaxLength(255).IsRequired();
            entity.Property(e => e.LocalBindAddress).HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<CredentialEntity>(entity =>
        {
            entity.ToTable("Credentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(128).IsRequired();
            entity.Property(e => e.EncryptedPassword).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<SettingEntity>(entity =>
        {
            entity.ToTable("Settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }
}
