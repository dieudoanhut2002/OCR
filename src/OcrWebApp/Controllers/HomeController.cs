using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcrWebApp.Data;
using OcrWebApp.Middlewares;
using OcrWebApp.Models.ViewModels;

namespace OcrWebApp.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sessionToken = HttpContext.Items[UserSessionMiddleware.SessionItemKey]?.ToString() ?? string.Empty;

        var recentJobs = await _db.Jobs
            .Where(j => j.SessionToken == sessionToken)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(5)
            .ToListAsync();

        var vm = new HomeViewModel
        {
            DefaultLanguage = "vie",
            RecentJobs = recentJobs
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
