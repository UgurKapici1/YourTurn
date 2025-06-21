using Microsoft.EntityFrameworkCore;
using YourTurn.Web.Models;

namespace YourTurn.Web.Data
{
    // Veritabanı bağlantısını ve tablo eşlemelerini yöneten ana sınıf
    public class YourTurnDbContext : DbContext
    {
        // Kurucu metot, veritabanı seçeneklerini alır
        public YourTurnDbContext(DbContextOptions<YourTurnDbContext> options) : base(options)
        {
        }

        // Yönetici ile ilgili veritabanı tabloları
        public DbSet<Admin> Admins { get; set; }
        public DbSet<AdminLog> AdminLogs { get; set; }
        public DbSet<AdminSetting> AdminSettings { get; set; }

        // Oyun ile ilgili veritabanı tabloları
        public DbSet<PlayerStat> PlayerStats { get; set; }
        public DbSet<LobbyHistory> LobbyHistories { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Answer> Answers { get; set; }

        // Model oluşturulurken veritabanı şemasını yapılandırır
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Admin entity'si için yapılandırma
            modelBuilder.Entity<Admin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("Admin");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Kullanıcı adının benzersiz olmasını sağlar
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // AdminLog entity'si için yapılandırma
            modelBuilder.Entity<AdminLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Yabancı anahtar ilişkisi
                entity.HasOne(e => e.Admin)
                    .WithMany(e => e.AdminLogs)
                    .HasForeignKey(e => e.AdminId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AdminSetting entity'si için yapılandırma
            modelBuilder.Entity<AdminSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Ayar anahtarının benzersiz olmasını sağlar
                entity.HasIndex(e => e.SettingKey).IsUnique();
                
                // Yabancı anahtar ilişkisi
                entity.HasOne(e => e.UpdatedByAdmin)
                    .WithMany(e => e.AdminSettings)
                    .HasForeignKey(e => e.UpdatedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PlayerStat entity'si için yapılandırma
            modelBuilder.Entity<PlayerStat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PlayerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Oyuncu adının benzersiz olmasını sağlar
                entity.HasIndex(e => e.PlayerName).IsUnique();
            });

            // LobbyHistory entity'si için yapılandırma
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

            // Category entity'si için yapılandırma
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // Question entity'si için yapılandırma
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Text).IsRequired();
                
                entity.HasOne(q => q.Category)
                    .WithMany(c => c.Questions)
                    .HasForeignKey(q => q.CategoryId);
            });

            // Answer entity'si için yapılandırma
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Text).IsRequired();

                entity.HasOne(a => a.Question)
                    .WithMany(q => q.Answers)
                    .HasForeignKey(a => a.QuestionId);
            });

            // Varsayılan yönetici için başlangıç verisi
            modelBuilder.Entity<Admin>().HasData(new Admin
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), // Varsayılan şifre
                Email = "admin@yourturn.com",
                Role = "SuperAdmin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            // Varsayılan ayarlar için başlangıç verileri
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