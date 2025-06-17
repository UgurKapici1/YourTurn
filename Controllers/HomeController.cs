using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YourTurn.Web.Models;

namespace YourTurn.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Displays the home page
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Displays the join lobby page
    /// </summary>
    [HttpGet]
    public IActionResult JoinLobby()
    {
        return View();
    }

    /// <summary>
    /// Handles player name submission and redirects to join lobby
    /// </summary>
    [HttpPost]
    public IActionResult Start(string playerName)
    {
        TempData["PlayerName"] = playerName;
        return RedirectToAction("JoinLobby");
    }

    /// <summary>
    /// Displays the privacy page
    /// </summary>
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Displays error information
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
