using YourTurn.Web.Hubs;
using YourTurn.Web.Services;
using YourTurn.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

// Uygulama oluşturucusu başlatılıyor
var builder = WebApplication.CreateBuilder(args);

// Servisler konteynere ekleniyor
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSession();

// Entity Framework Core ve PostgreSQL veritabanı bağlantısı ekleniyor
builder.Services.AddDbContext<YourTurnDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Kimlik doğrulama ve yetkilendirme servisleri ekleniyor
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.AccessDeniedPath = "/Admin/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Arka plan servisleri ekleniyor
builder.Services.AddHostedService<PeerHostingService>();

// Uygulama oluşturuluyor
var app = builder.Build();

// HTTP istek işlem hattı yapılandırılıyor
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Varsayılan HSTS değeri 30 gündür. Üretim senaryoları için bunu değiştirmek isteyebilirsiniz, bkz. https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// SignalR hub'ları eşleniyor
app.MapHub<LobbyHub>("/lobbyHub");
app.MapHub<GameHub>("/GameHub");
app.UseSession();

app.UseHttpsRedirection();
app.UseRouting();

// Kimlik doğrulama ve yetkilendirme middleware'leri kullanılıyor
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Varsayılan rota yapılandırılıyor
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Uygulama çalıştırılıyor
app.Run();
