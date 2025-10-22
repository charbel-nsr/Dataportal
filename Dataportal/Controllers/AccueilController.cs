using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dataportal.Controllers;

public class AccueilController : Controller
{
    private readonly ILogger<AccueilController> _logger;
    private readonly ApplicationDbContext _context;

    public AccueilController(ILogger<AccueilController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var latestMetadonnees = await _context.Metadonnee
            .AsNoTracking()
            .Include(m => m.TypeEnergieRenouvelable)
            .Where(m => m.IdVisibilite == VisibiliteIds.Public)
            .OrderByDescending(m => m.DernierMiseAJour ?? DateTime.MinValue)
            .ThenByDescending(m => m.Id)
            .Take(3)
            .ToListAsync();

        var viewModel = new AccueilIndexViewModel
        {
            LatestDatasets = latestMetadonnees
                .Select(m => new LatestDatasetViewModel
                {
                    Id = m.Id,
                    Name = m.Nom,
                    EnergyType = m.TypeEnergieRenouvelable?.Libelle,
                    IconName = ResolveIconName(m.TypeEnergieRenouvelable?.Libelle)
                })
                .ToList()
        };

        return View(viewModel);
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

    [Route("Accueil/Status/{code}")]
    public IActionResult Status(int code)
    {
        ViewData["StatusCode"] = code;
        if (code >= 400 && code < 500)
        {
            return View("40X");
        }
        return View("50X");
    }

    private static string ResolveIconName(string? energyType)
    {
        if (string.IsNullOrWhiteSpace(energyType))
        {
            return "energy_savings_leaf";
        }

        var normalized = energyType.Trim().ToLowerInvariant();

        if (normalized.Contains("wind") || normalized.Contains("éolien") || normalized.Contains("eolien"))
        {
            return "wind_power";
        }

        if (normalized.Contains("solar") || normalized.Contains("solaire") || normalized.Contains("photovolta"))
        {
            return "solar_power";
        }

        if (normalized.Contains("hydro") || normalized.Contains("water") || normalized.Contains("hydroé") || normalized.Contains("hydroe"))
        {
            return "water_ec";
        }

        return "energy_savings_leaf";
    }
}
