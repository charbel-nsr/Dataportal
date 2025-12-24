using System.Text;
using Dataportal.Classes;
using Dataportal.Models;
using Microsoft.Extensions.Options;

namespace Dataportal.Services.Email;

public interface IAccountEmailService
{
    Task SendAccountRequestVerificationAsync(DemandeDeCompte demande, string verificationLink, CancellationToken cancellationToken = default);
    Task SendAccountRequestApprovedAsync(string email, string role, string? entreprise, CancellationToken cancellationToken = default);
    Task SendActivationChangeAsync(Utilisateur user, bool isActive, CancellationToken cancellationToken = default);
    Task SendForgotPasswordAsync(Utilisateur user, string resetLink, CancellationToken cancellationToken = default);
    Task SendMfaCodeAsync(Utilisateur user, string code, CancellationToken cancellationToken = default);
    Task SendPendingRequestsReminderAsync(IEnumerable<string> adminEmails, IEnumerable<DemandeDeCompte> demandes, string portalUrl, CancellationToken cancellationToken = default);
    Task SendPasswordChangedAsync(Utilisateur user, CancellationToken cancellationToken = default);
}

public class AccountEmailService : IAccountEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly PortalOptions _portalOptions;
    private readonly ILogger<AccountEmailService> _logger;

    public AccountEmailService(IEmailSender emailSender, IEmailTemplateRenderer renderer, IOptions<PortalOptions> portalOptions, ILogger<AccountEmailService> logger)
    {
        _emailSender = emailSender;
        _renderer = renderer;
        _portalOptions = portalOptions.Value;
        _logger = logger;
    }

    public Task SendAccountRequestVerificationAsync(DemandeDeCompte demande, string verificationLink, CancellationToken cancellationToken = default)
    {
        var body = new StringBuilder();
        body.Append("<p>Hello ")
            .Append(demande.Prenom)
            .Append(",</p>");
        body.Append("<p>Thanks for requesting an account. Please confirm you own this email address by using the button below:</p>");
        body.Append($"""<p style="text-align:center;margin:24px 0;"><a href="{verificationLink}" style="background:#2563eb;color:#fff;padding:12px 18px;border-radius:10px;text-decoration:none;font-weight:600;">Confirm my email</a></p>""");
        body.Append("<p>This link is valid for 48 hours. If you did not request this, you can ignore this message.</p>");

        var html = _renderer.RenderHtml("Confirm your email address", body.ToString());
        return _emailSender.SendEmailAsync(demande.Email, "Confirm your email address", html, cancellationToken: cancellationToken);
    }

    public Task SendAccountRequestApprovedAsync(string email, string role, string? entreprise, CancellationToken cancellationToken = default)
    {
        var body = new StringBuilder();
        body.Append("<p>Hello,</p>");
        body.Append("<p>Good news! Your account request has been approved.</p>");
        body.Append($"<p>Assigned role: <strong>{role}</strong></p>");
        if (!string.IsNullOrWhiteSpace(entreprise))
        {
            body.Append($"<p>Organization: <strong>{entreprise}</strong></p>");
        }
        body.Append($"<p>You can sign in now: <a href=\"{GetPortalUrl()}/Compte/SeConnecter\">Open Dataportal</a></p>");

        var html = _renderer.RenderHtml("Your account has been approved", body.ToString());
        return _emailSender.SendEmailAsync(email, "Your account has been approved", html, cancellationToken: cancellationToken);
    }

    public Task SendActivationChangeAsync(Utilisateur user, bool isActive, CancellationToken cancellationToken = default)
    {
        var body = new StringBuilder();
        body.Append("<p>Hello ").Append(user.Prenom).Append(" ").Append(user.Nom).Append(",</p>");
        body.Append(isActive
            ? "<p>Your account has been reactivated. You can sign in now.</p>"
            : "<p>Your account has been deactivated. Contact an administrator if you believe this is in error.</p>");
        body.Append($"<p><a href=\"{GetPortalUrl()}/Compte/SeConnecter\">Open Dataportal</a></p>");

        var subject = isActive ? "Your account has been reactivated" : "Your account has been deactivated";
        var html = _renderer.RenderHtml(subject, body.ToString());
        return _emailSender.SendEmailAsync(user.Email, subject, html, cancellationToken: cancellationToken);
    }

    public Task SendForgotPasswordAsync(Utilisateur user, string resetLink, CancellationToken cancellationToken = default)
    {
        var body = new StringBuilder();
        body.Append("<p>Hello ").Append(user.Prenom).Append(",</p>");
        body.Append("<p>We received a password reset request for your account.</p>");
        body.Append($"""<p style="text-align:center;margin:24px 0;"><a href="{resetLink}" style="background:#2563eb;color:#fff;padding:12px 18px;border-radius:10px;text-decoration:none;font-weight:600;">Reset my password</a></p>""");
        body.Append("<p>This link is valid for 60 minutes. If you didn't make this request, you can ignore this message.</p>");

        var html = _renderer.RenderHtml("Reset your password", body.ToString());
        return _emailSender.SendEmailAsync(user.Email, "Reset your password", html, cancellationToken: cancellationToken);
    }

    public Task SendMfaCodeAsync(Utilisateur user, string code, CancellationToken cancellationToken = default)
    {
        var body = new StringBuilder();
        body.Append("<p>Hello ").Append(user.Prenom).Append(",</p>");
        body.Append("<p>Here is your verification code to complete sign-in:</p>");
        body.Append($"<p style=\"font-size:28px;font-weight:700;letter-spacing:6px;text-align:center;\">{code}</p>");
        body.Append("<p>This code expires in 10 minutes.</p>");

        var html = _renderer.RenderHtml("Your sign-in code", body.ToString());
        return _emailSender.SendEmailAsync(user.Email, "Your sign-in code", html, cancellationToken: cancellationToken);
    }

    public Task SendPendingRequestsReminderAsync(IEnumerable<string> adminEmails, IEnumerable<DemandeDeCompte> demandes, string portalUrl, CancellationToken cancellationToken = default)
    {
        var list = demandes.ToList();
        if (!list.Any() || !adminEmails.Any())
        {
            return Task.CompletedTask;
        }

        var body = new StringBuilder();
        body.Append("<p>Hello,</p>");
        body.Append("<p>There are pending account requests with verified emails. Please review them:</p>");
        body.Append("<ul>");
        foreach (var demande in list)
        {
            body.Append("<li>")
                .Append($"{demande.Prenom} {demande.Nom} - {demande.Email} (created on {demande.DateCreation:yyyy-MM-dd})")
                .Append("</li>");
        }
        body.Append("</ul>");
        body.Append($"<p>Manage requests: <a href=\"{portalUrl}/Gestion/DemandeDeCompte\">Open Dataportal</a></p>");

        var html = _renderer.RenderHtml("Pending account requests", body.ToString());
        var sendTasks = adminEmails.Select(email => _emailSender.SendEmailAsync(email, "Pending account requests", html, cancellationToken: cancellationToken));

        return Task.WhenAll(sendTasks);
    }

    public Task SendPasswordChangedAsync(Utilisateur user, CancellationToken cancellationToken = default)
    {
        var body = new StringBuilder();
        body.Append("<p>Hello ").Append(user.Prenom).Append(" ").Append(user.Nom).Append(",</p>");
        body.Append("<p>Your Dataportal account password was changed.</p>");
        body.Append("<p>If you didn't make this change, reset your password immediately and contact an administrator.</p>");
        body.Append($"<p><a href=\"{GetPortalUrl()}/Compte/MotDePasseOublie\">Reset my password</a></p>");

        var html = _renderer.RenderHtml("Your password was changed", body.ToString());
        return _emailSender.SendEmailAsync(user.Email, "Your password was changed", html, cancellationToken: cancellationToken);
    }

    private string GetPortalUrl()
    {
        return _portalOptions.PublicBaseUrl?.TrimEnd('/') ?? string.Empty;
    }
}