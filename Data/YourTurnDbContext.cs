using Microsoft.EntityFrameworkCore;
using YourTurn.Web.Models;

namespace YourTurn.Web.Data
{
    public class YourTurnDbContext : DbContext
    {
        public YourTurnDbContext(DbContextOptions<YourTurnDbContext> options) : base(options)
        {
        }

        // Admin related DbSets
        public DbSet<Admin> Admins { get; set; }
        public DbSet<AdminLog> AdminLogs { get; set; }
        public DbSet<AdminSetting> AdminSettings { get; set; }

        // Game related DbSets
        public DbSet<PlayerStat> PlayerStats { get; set; }
        public DbSet<LobbyHistory> LobbyHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Admin entity configuration
            modelBuilder.Entity<Admin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("Admin");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Unique constraint on username
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // AdminLog entity configuration
            modelBuilder.Entity<AdminLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Foreign key relationship
                entity.HasOne(e => e.Admin)
                    .WithMany(e => e.AdminLogs)
                    .HasForeignKey(e => e.AdminId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AdminSetting entity configuration
            modelBuilder.Entity<AdminSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Unique constraint on setting key
                entity.HasIndex(e => e.SettingKey).IsUnique();
                
                // Foreign key relationship
                entity.HasOne(e => e.UpdatedByAdmin)
                    .WithMany(e => e.AdminSettings)
                    .HasForeignKey(e => e.UpdatedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PlayerStat entity configuration
            modelBuilder.Entity<PlayerStat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PlayerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Unique constraint on player name
                entity.HasIndex(e => e.PlayerName).IsUnique();
            });

            // LobbyHistory entity configuration
            modelBuilder.Entity<LobbyHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LobbyCode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.HostPlayerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Category).HasMaxLength(50);
                entity.Property(e => e.Winner).HasMaxLength(10);
                entity.Property(e => e.FinalScore).HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Seed data for default admin
            modelBuilder.Entity<Admin>().HasData(new Admin
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), // Default password
                Email = "admin@yourturn.com",
                Role = "SuperAdmin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            // Seed data for default settings
            modelBuilder.Entity<AdminSetting>().HasData(
                new AdminSetting
                {
                    Id = 1,
                    SettingKey = "WinningScore",
                    SettingValue = "5",
                    Description = "Oyunu kazanmak için gereken skor",
                    UpdatedAt = DateTime.UtcNow
                },
                new AdminSetting
                {
                    Id = 2,
                    SettingKey = "TimerSpeed",
                    SettingValue = "0.2",
                    Description = "Süre çubuğunun hareket hızı",
                    UpdatedAt = DateTime.UtcNow
                },
                new AdminSetting
                {
                    Id = 3,
                    SettingKey = "MaxPlayersPerLobby",
                    SettingValue = "10",
                    Description = "Lobi başına maksimum oyuncu sayısı",
                    UpdatedAt = DateTime.UtcNow
                }
            );
        }
    }
} 