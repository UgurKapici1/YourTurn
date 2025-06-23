using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YourTurn.Web.Data;
using YourTurn.Web.Models;
using YourTurn.Web.Stores;
using BCrypt.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace YourTurn.Web.Controllers
{
    // Yönetici paneli ile ilgili tüm istekleri yönetir
    public class AdminController : Controller
    {
        private readonly YourTurnDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Gerekli servisleri enjekte eder
        public AdminController(YourTurnDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // GET: /admin için varsayılan rota
        [Route("admin")]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard");
            }
            return RedirectToAction("Login");
        }

        // GET: Yönetici giriş sayfasını görüntüler
        [Route("admin/login")]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // POST: Yönetici giriş işlemini gerçekleştirir
        [HttpPost]
        [Route("admin/login")]
        public async Task<IActionResult> Login(string username, string password)
        {
            var admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.Username == username && a.IsActive);

            if (admin != null && BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
            {
                // Update last login
                admin.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Log login action
                await LogAdminAction(admin.Id, "Login", "Başarılı giriş");

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, admin.Username),
                    new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                    new Claim(ClaimTypes.Role, admin.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                return RedirectToAction("Dashboard");
            }

            ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre");
            return View();
        }

        // GET: Yönetici çıkış işlemini gerçekleştirir
        [Route("admin/logout")]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await LogAdminAction(adminId, "Logout", "Çıkış yapıldı");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: Yönetici kontrol panelini görüntüler
        [Route("admin/dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            // Get active lobbies and players from in-memory store
            var activeLobbies = LobbyStore.GetActiveLobbies();
            var activePlayers = LobbyStore.PlayerToConnections.Keys;

            var dashboardData = new AdminDashboardViewModel
            {
                TotalPlayers = await _context.PlayerStats.CountAsync(),
                TotalLobbies = await _context.LobbyHistories.CountAsync(),
                ActiveLobbies = activeLobbies.Count,
                ActivePlayers = activePlayers.Count,
                RecentLogs = await _context.AdminLogs
                    .Include(l => l.Admin)
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(10)
                    .ToListAsync(),
                CurrentActiveLobbies = activeLobbies,
                CurrentActivePlayers = activePlayers.ToList()
            };

            return View(dashboardData);
        }

        // GET: Kontrol paneli için anlık verileri JSON formatında sağlar
        [Route("admin/dashboard-data")]
        public IActionResult DashboardData()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            // Get active lobbies and players from in-memory store
            var activeLobbies = LobbyStore.GetActiveLobbies();
            var activePlayers = LobbyStore.PlayerToConnections.Keys;

            var dashboardData = new
            {
                activeLobbies = activeLobbies.Count,
                activePlayers = activePlayers.Count,
                currentActiveLobbies = activeLobbies.Select(l => new
                {
                    lobbyCode = l.LobbyCode,
                    hostPlayerName = l.HostPlayerName,
                    playerCount = l.Players.Count,
                    isGameStarted = l.IsGameStarted,
                    category = l.Category,
                    createdAt = l.CreatedAt.ToString("dd.MM.yyyy HH:mm")
                }).ToList(),
                currentActivePlayers = activePlayers.ToList()
            };

            return Json(dashboardData);
        }

        // GET: Şifre değiştirme sayfasını görüntüler
        [Route("admin/change-password")]
        public IActionResult ChangePassword()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        // POST: Yönetici şifresini değiştirir
        [HttpPost]
        [Route("admin/change-password")]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Yeni şifreler eşleşmiyor");
                return View();
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Yeni şifre en az 6 karakter olmalıdır");
                return View();
            }

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var admin = await _context.Admins.FindAsync(adminId);

            if (admin == null)
            {
                return RedirectToAction("Login");
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, admin.PasswordHash))
            {
                ModelState.AddModelError("", "Mevcut şifre yanlış");
                return View();
            }

            // Update password
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            // Log password change
            await LogAdminAction(adminId, "ChangePassword", "Şifre değiştirildi");

            TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirildi!";
            return RedirectToAction("Dashboard");
        }

        // GET: Kullanıcı adı değiştirme sayfasını görüntüler
        [Route("admin/change-username")]
        public IActionResult ChangeUsername()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        // POST: Yönetici kullanıcı adını değiştirir
        [HttpPost]
        [Route("admin/change-username")]
        public async Task<IActionResult> ChangeUsername(string currentPassword, string newUsername)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(newUsername) || newUsername.Length < 3)
            {
                ModelState.AddModelError("", "Yeni kullanıcı adı en az 3 karakter olmalıdır");
                return View();
            }

            // Check if username already exists
            var existingAdmin = await _context.Admins.FirstOrDefaultAsync(a => a.Username == newUsername);
            if (existingAdmin != null)
            {
                ModelState.AddModelError("", "Bu kullanıcı adı zaten kullanılıyor");
                return View();
            }

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var admin = await _context.Admins.FindAsync(adminId);

            if (admin == null)
            {
                return RedirectToAction("Login");
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, admin.PasswordHash))
            {
                ModelState.AddModelError("", "Mevcut şifre yanlış");
                return View();
            }

            var oldUsername = admin.Username;
            // Update username
            admin.Username = newUsername;
            await _context.SaveChangesAsync();

            // Log username change
            await LogAdminAction(adminId, "ChangeUsername", $"Kullanıcı adı değiştirildi: {oldUsername} -> {newUsername}");

            // Sign out and redirect to login to refresh the session
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "Kullanıcı adınız başarıyla değiştirildi! Lütfen yeni kullanıcı adınızla tekrar giriş yapın.";
            return RedirectToAction("Login");
        }

        // GET: Oyuncu listesini görüntüler
        [Route("admin/players")]
        public async Task<IActionResult> Players()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            // Get active players from in-memory store
            var activePlayers = LobbyStore.PlayerToConnections.Keys;
            var dbPlayers = await _context.PlayerStats
                .OrderByDescending(p => p.LastSeenAt ?? p.CreatedAt)
                .ToListAsync();

            var viewModel = new AdminPlayersViewModel
            {
                ActivePlayers = activePlayers.ToList(),
                DatabasePlayers = dbPlayers
            };

            return View(viewModel);
        }

        // GET: Lobi listesini görüntüler
        [Route("admin/lobbies")]
        public async Task<IActionResult> Lobbies()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            // Get active lobbies from in-memory store
            var activeLobbies = LobbyStore.GetActiveLobbies();
            var dbLobbies = await _context.LobbyHistories
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            var viewModel = new AdminLobbiesViewModel
            {
                ActiveLobbies = activeLobbies,
                DatabaseLobbies = dbLobbies
            };

            return View(viewModel);
        }

        // GET: Yönetici ayarlarını görüntüler
        [Route("admin/settings")]
        public async Task<IActionResult> Settings()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var settings = await _context.AdminSettings
                .Include(s => s.UpdatedByAdmin)
                .OrderBy(s => s.SettingKey)
                .ToListAsync();

            return View(settings);
        }

        // POST: Bir yönetici ayarını günceller
        [HttpPost]
        [Route("admin/update-setting")]
        public async Task<IActionResult> UpdateSetting(int id, string value)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var setting = await _context.AdminSettings.FindAsync(id);
            if (setting != null)
            {
                var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                setting.SettingValue = value;
                setting.UpdatedBy = adminId;
                setting.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await LogAdminAction(adminId, "UpdateSetting", $"Ayar güncellendi: {setting.SettingKey} = {value}");
            }

            return RedirectToAction("Settings");
        }

        // GET: Yönetici eylem günlüklerini görüntüler
        [Route("admin/logs")]
        public async Task<IActionResult> Logs(int page = 1, int pageSize = 50)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var query = _context.AdminLogs
                .Include(l => l.Admin)
                .OrderByDescending(l => l.CreatedAt);

            var totalCount = await query.CountAsync();
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new AdminLogsViewModel
            {
                Logs = logs,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return View(viewModel);
        }

        [Route("admin/categories")]
        public async Task<IActionResult> Categories()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var categories = await _context.Categories
                .Include(c => c.Questions)
                .ThenInclude(q => q.Answers)
                .ToListAsync();

            var viewModel = new AdminCategoriesViewModel
            {
                Categories = categories
            };

            return View(viewModel);
        }

        [HttpPost]
        [Route("admin/categories/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory([FromForm] Category model)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            var errorMessage = "Kategori eklenirken bir hata oluştu: " + string.Join(", ", errors);
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        [Route("admin/questions/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestion([FromForm] Question model)
        {
            if (ModelState.IsValid)
            {
                await _context.Questions.AddAsync(model);
                await _context.SaveChangesAsync();
                await LogAdminAction(GetAdminId(), "AddQuestion", $"Soru eklendi: {model.Text}");
                return Json(new { success = true, message = "Soru başarıyla eklendi." });
            }
            // Log validation errors
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            await LogAdminAction(GetAdminId(), "AddQuestionError", $"Soru eklenemedi: {string.Join(", ", errors)}");

            return Json(new { success = false, message = "Soru eklenemedi.", errors = errors });
        }

        [HttpPost]
        [Route("admin/answers/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAnswer([FromForm] Answer model)
        {
            if (ModelState.IsValid)
            {
                await _context.Answers.AddAsync(model);
                await _context.SaveChangesAsync();
                await LogAdminAction(GetAdminId(), "AddAnswer", $"Cevap eklendi: {model.Text}");
                return Json(new { success = true, message = "Cevap başarıyla eklendi." });
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Cevap eklenemedi.", errors = errors });
        }

        [HttpPost("admin/categories/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return Json(new { success = false, message = "Kategori bulunamadı." });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            await LogAdminAction(GetAdminId(), "DeleteCategory", $"Kategori silindi: {category.Name}");
            return Json(new { success = true, message = "Kategori başarıyla silindi." });
        }

        [HttpPost("admin/questions/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question == null)
            {
                return Json(new { success = false, message = "Soru bulunamadı." });
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();
            await LogAdminAction(GetAdminId(), "DeleteQuestion", $"Soru silindi: {question.Text}");
            return Json(new { success = true, message = "Soru başarıyla silindi." });
        }

        [HttpPost("admin/answers/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAnswer(int id)
        {
            var answer = await _context.Answers.FindAsync(id);
            if (answer == null)
            {
                return Json(new { success = false, message = "Cevap bulunamadı." });
            }

            _context.Answers.Remove(answer);
            await _context.SaveChangesAsync();
            await LogAdminAction(GetAdminId(), "DeleteAnswer", $"Cevap silindi: {answer.Text}");
            return Json(new { success = true, message = "Cevap başarıyla silindi." });
        }

        [HttpPost("admin/categories/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory([FromForm] Category model)
        {
            if (ModelState.IsValid)
            {
                var category = await _context.Categories.FindAsync(model.Id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Kategori bulunamadı." });
                }

                category.Name = model.Name;
                _context.Categories.Update(category);
                await _context.SaveChangesAsync();
                await LogAdminAction(GetAdminId(), "EditCategory", $"Kategori güncellendi: {category.Name}");
                return Json(new { success = true, message = "Kategori başarıyla güncellendi." });
            }
            return Json(new { success = false, message = "Model geçerli değil." });
        }

        [HttpPost("admin/questions/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion([FromForm] Question model)
        {
            if (ModelState.IsValid)
            {
                var question = await _context.Questions.FindAsync(model.Id);
                if (question == null)
                {
                    return Json(new { success = false, message = "Soru bulunamadı." });
                }

                question.Text = model.Text;
                _context.Questions.Update(question);
                await _context.SaveChangesAsync();
                await LogAdminAction(GetAdminId(), "EditQuestion", $"Soru güncellendi: {question.Text}");
                return Json(new { success = true, message = "Soru başarıyla güncellendi." });
            }
            return Json(new { success = false, message = "Model geçerli değil." });
        }

        [HttpPost("admin/answers/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAnswer([FromForm] Answer model)
        {
            if (ModelState.IsValid)
            {
                var answer = await _context.Answers.FindAsync(model.Id);
                if (answer == null)
                {
                    return Json(new { success = false, message = "Cevap bulunamadı." });
                }

                answer.Text = model.Text;
                answer.IsCorrect = model.IsCorrect;
                _context.Answers.Update(answer);
                await _context.SaveChangesAsync();
                await LogAdminAction(GetAdminId(), "EditAnswer", $"Cevap güncellendi: {answer.Text}");
                return Json(new { success = true, message = "Cevap başarıyla güncellendi." });
            }
            return Json(new { success = false, message = "Model geçerli değil." });
        }

        private int GetAdminId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        // Yönetici eylemlerini veritabanına kaydeder
        private async Task LogAdminAction(int adminId, string action, string details = null)
        {
            var log = new AdminLog
            {
                AdminId = adminId,
                Action = action,
                Details = details,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString(),
                CreatedAt = DateTime.UtcNow
            };

            _context.AdminLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }

    // Yönetici kontrol paneli için view model
    public class AdminDashboardViewModel
    {
        public int TotalPlayers { get; set; }
        public int TotalLobbies { get; set; }
        public int ActiveLobbies { get; set; }
        public int ActivePlayers { get; set; }
        public List<AdminLog> RecentLogs { get; set; } = new List<AdminLog>();
        public List<Lobby> CurrentActiveLobbies { get; set; } = new List<Lobby>();
        public List<string> CurrentActivePlayers { get; set; } = new List<string>();
    }

    // Oyuncular sayfası için view model
    public class AdminPlayersViewModel
    {
        public List<string> ActivePlayers { get; set; } = new List<string>();
        public List<PlayerStat> DatabasePlayers { get; set; } = new List<PlayerStat>();
    }

    // Lobiler sayfası için view model
    public class AdminLobbiesViewModel
    {
        public List<Lobby> ActiveLobbies { get; set; } = new List<Lobby>();
        public List<LobbyHistory> DatabaseLobbies { get; set; } = new List<LobbyHistory>();
    }

    // Günlükler sayfası için view model
    public class AdminLogsViewModel
    {
        public List<AdminLog> Logs { get; set; } = new List<AdminLog>();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class AdminCategoriesViewModel
    {
        public List<Category> Categories { get; set; } = new List<Category>();
    }
} 