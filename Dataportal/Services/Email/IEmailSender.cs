namespace Dataportal.Services.Email;

public interface IEmailSender
{
    Task SendEmailAsync(string recipientEmail, string subject, string htmlBody, string? replyTo = null, CancellationToken cancellationToken = default);
}