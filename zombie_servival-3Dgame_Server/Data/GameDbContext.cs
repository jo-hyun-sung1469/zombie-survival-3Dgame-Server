using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Auth;
using zombie_servival_3Dgame_Server.Inventory;

namespace zombie_servival_3Dgame_Server.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PlayerSaveData> PlayerSaveData => Set<PlayerSaveData>();
    public DbSet<PlayerWeaponState> PlayerWeaponStates => Set<PlayerWeaponState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.Property(x => x.Id).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserName).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
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
        });

        modelBuilder.Entity<PlayerWeaponState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WeaponName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IsOwned).IsRequired();
        });
    }
}
