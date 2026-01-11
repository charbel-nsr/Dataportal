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
        var baseQuery = _context.Metadonnee
            .AsNoTracking()
            .Include(m => m.TypeEnergieRenouvelable)
            .Include(m => m.Utilisateur)
                .ThenInclude(u => u.Entreprise)
            .AsQueryable();

        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            baseQuery = baseQuery.Where(m => m.IdVisibilite == VisibiliteIds.Public);
        }
        else
        {
            var currentUserId = HttpContextUserHelper.TryGetCurrentUserId(User);
            var currentUserRole = HttpContextUserHelper.GetCurrentUserRole(HttpContext, _context);
            var currentUserEntrepriseId = HttpContextUserHelper.GetCurrentUserEntrepriseId(HttpContext, _context);

            var isAdmin = currentUserRole == RoleIds.Administrateur;
            var isInternalRole = currentUserRole == RoleIds.Utilisateur || currentUserRole == RoleIds.Editeur;

            baseQuery = baseQuery.Where(m =>
                m.IdVisibilite == VisibiliteIds.Public ||
                m.IdVisibilite == VisibiliteIds.Prive ||
                (m.IdVisibilite == VisibiliteIds.Interne &&
                    (isAdmin ||
                        (isInternalRole &&
                            currentUserEntrepriseId.HasValue &&
                            m.Utilisateur != null &&
                            m.Utilisateur.IdEntreprise == currentUserEntrepriseId))) ||
                (m.IdVisibilite == VisibiliteIds.Personnelle &&
                    (isAdmin || (currentUserId.HasValue && m.IdUtilisateur == currentUserId.Value))));
        }

        var latestMetadonnees = await baseQuery
            .OrderByDescending(m => m.DernierMiseAJour ?? DateTime.MinValue)
            .ThenByDescending(m => m.Id)
            .Take(6)
            .ToListAsync();

        var messageAccueil = await _context.MessageAccueil
            .AsNoTracking()
            .OrderByDescending(m => m.DateDerniereModification)
            .FirstOrDefaultAsync();

        var shouldShowMessage = messageAccueil != null &&
            !string.IsNullOrWhiteSpace(messageAccueil.Contenu) &&
            ((User.Identity?.IsAuthenticated ?? false) || messageAccueil.VisibleAuxInvites);

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
                .ToList(),
            MessageAccueil = shouldShowMessage ? new AccueilMessageViewModel
            {
                Contenu = messageAccueil!.Contenu,
                VisibleAuxInvites = messageAccueil.VisibleAuxInvites
            } : null
        };

        return View(viewModel);
    }

    public IActionResult Politiques()
    {
        return View();
    }

    public IActionResult NotebookApi()
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
