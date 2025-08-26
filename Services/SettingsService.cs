using Microsoft.Extensions.Caching.Memory;
using YourTurn.Web.Data;
using YourTurn.Web.Interfaces;

namespace YourTurn.Web.Services
{
    // Veritabanından uygulama ayarlarını okuyan servis
    public class SettingsService : ISettingsService
    {
        private readonly YourTurnDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKeyWinningScore = "settings:WinningScore";
        private const string CacheKeyTimerSpeed = "settings:TimerSpeed";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

        public SettingsService(YourTurnDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public int GetWinningScore()
        {
            if (_cache.TryGetValue(CacheKeyWinningScore, out int cached)) return cached;
            var setting = _context.AdminSettings.FirstOrDefault(s => s.SettingKey == "WinningScore");
            var value = setting != null && int.TryParse(setting.SettingValue, out var parsed) ? parsed : 5;
            _cache.Set(CacheKeyWinningScore, value, CacheDuration);
            return value;
        }

        public double GetTimerSpeed()
        {
            if (_cache.TryGetValue(CacheKeyTimerSpeed, out double cached)) return cached;
            var setting = _context.AdminSettings.FirstOrDefault(s => s.SettingKey == "TimerSpeed");
            var value = setting != null && double.TryParse(setting.SettingValue, out var parsed) ? parsed : 0.2;
            _cache.Set(CacheKeyTimerSpeed, value, CacheDuration);
            return value;
        }
    }
}

