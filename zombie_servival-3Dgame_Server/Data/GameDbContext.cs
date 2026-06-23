using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Firearm.Models;
using zombie_survival_3Dgame_Server.Inventory.Models;
using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuthVerificationCode> AuthVerificationCodes => Set<AuthVerificationCode>();
    public DbSet<PlayerSaveData> PlayerSaveData => Set<PlayerSaveData>();
    public DbSet<PlayerWeaponState> PlayerWeaponStates => Set<PlayerWeaponState>();
    public DbSet<PlayerStatUpgradeState> PlayerStatUpgradeStates => Set<PlayerStatUpgradeState>();
    public DbSet<FirearmDefinition> FirearmDefinitions => Set<FirearmDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Id).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserName).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AuthVerificationCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email);
            entity.Property(x => x.Id).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.Property(x => x.CodeHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.AttemptCount).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
        });

        modelBuilder.Entity<PlayerSaveData>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PlayerId).IsUnique();
            entity.Property(x => x.PlayerId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Gold).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasMany(x => x.WeaponStates)
                .WithOne(x => x.PlayerSaveData)
                .HasForeignKey(x => x.PlayerSaveDataId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.StatUpgradeStates)
                .WithOne(x => x.PlayerSaveData)
                .HasForeignKey(x => x.PlayerSaveDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerWeaponState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PlayerSaveDataId, x.FirearmDefinitionId }).IsUnique();
            entity.HasIndex(x => x.FirearmDefinitionId);
            entity.Property(x => x.FirearmDefinitionId).IsRequired();
            entity.Property(x => x.WeaponName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IsOwned).IsRequired();
            entity.Property(x => x.WeaponLevel).IsRequired();
            entity.Property(x => x.Damage).IsRequired();
            entity.Property(x => x.FireRate).IsRequired();
            entity.Property(x => x.MagazineSize).IsRequired();
            entity.Property(x => x.ReloadTimeSeconds).IsRequired();
            entity.Property(x => x.RangeMeters).IsRequired();
            entity.Property(x => x.CriticalMultiplier).IsRequired();

            entity.HasOne(x => x.FirearmDefinition)
                .WithMany()
                .HasForeignKey(x => x.FirearmDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FirearmDefinition>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Rarity).HasMaxLength(30).IsRequired();
            entity.Property(x => x.GachaProbability).IsRequired();
            entity.Property(x => x.Damage).IsRequired();
            entity.Property(x => x.FireRate).IsRequired();
            entity.Property(x => x.MagazineSize).IsRequired();
            entity.Property(x => x.ReloadTimeSeconds).IsRequired();
            entity.Property(x => x.RangeMeters).IsRequired();
            entity.Property(x => x.CriticalMultiplier).IsRequired();
        });

        modelBuilder.Entity<PlayerStatUpgradeState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PlayerSaveDataId, x.StatName }).IsUnique();
            entity.Property(x => x.StatName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.UpgradeLevel).IsRequired();
        });
    }
}
