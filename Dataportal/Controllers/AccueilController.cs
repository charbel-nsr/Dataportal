using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Dataportal.Models;

namespace Dataportal.Controllers;

//TODO: pages 40X and 50X

public class AccueilController : Controller
{
    private readonly ILogger<AccueilController> _logger;

    public AccueilController(ILogger<AccueilController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Politiques()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
