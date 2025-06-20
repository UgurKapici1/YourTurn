using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YourTurn.Web.Models;

namespace YourTurn.Web.Controllers;

// Ana sayfa ve diğer temel sayfalarla ilgili istekleri yönetir
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    // Gerekli servisleri enjekte eder
    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    // Ana sayfayı görüntüler
    public IActionResult Index()
    {
        return View();
    }

    // Lobiye katılma sayfasını görüntüler
    [HttpGet]
    public IActionResult JoinLobby()
    {
        return View();
    }

    // Oyuncu adını alır ve lobiye katılma sayfasına yönlendirir
    [HttpPost]
    public IActionResult Start(string playerName)
    {
        TempData["PlayerName"] = playerName;
        return RedirectToAction("JoinLobby");
    }

    // Gizlilik sayfasını görüntüler
    public IActionResult Privacy()
    {
        return View();
    }

    // Hata bilgilerini görüntüler
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
