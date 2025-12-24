using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dataportal.Services.Email;

public class PendingRequestReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingRequestReminderHostedService> _logger;
    private readonly PortalOptions _portalOptions;

    public PendingRequestReminderHostedService(IServiceScopeFactory scopeFactory, ILogger<PendingRequestReminderHostedService> logger, IOptions<PortalOptions> portalOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _portalOptions = portalOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run daily at 06:00 server time; only send on Wednesdays
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddHours(6);
            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday)
            {
                await SendRemindersAsync(stoppingToken);
            }
        }
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IAccountEmailService>();

        List<DemandeDeCompte> demandes;
        try
        {
            demandes = await db.DemandeDeCompte
                .Where(d => d.IdStatutDeLaDemande == StatutDeLaDemandeIds.EnAttente && d.EmailVerifie)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to fetch pending verified account requests for reminder.");
            return;
        }

        if (!demandes.Any())
        {
            return;
        }

        List<string> adminEmails;
        try
        {
            adminEmails = await db.Utilisateur
                .Where(u => u.IdRole == RoleIds.Administrateur && u.CompteActif)
                .Select(u => u.Email)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to fetch administrator emails for reminder.");
            return;
        }

        if (!adminEmails.Any())
        {
            return;
        }

        var portalUrl = (_portalOptions.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        await emailService.SendPendingRequestsReminderAsync(adminEmails, demandes, portalUrl, cancellationToken);
        _logger.LogInformation("Sent pending account request reminder to {AdminCount} administrators.", adminEmails.Count);
    }
}