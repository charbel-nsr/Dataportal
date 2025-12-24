using System.Net.Security;
using System.Security.Authentication;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Dataportal.Services.Email;

public class MailKitEmailSender : IEmailSender
{
    private readonly MailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<MailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string htmlBody, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("Mail host is not configured. Set it via environment variables for production.");
        }

        if (string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException("Mail from address is not configured.");
        }

        var message = BuildMessage(recipientEmail, subject, htmlBody, replyTo);

        using var client = new SmtpClient();

        client.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        client.ServerCertificateValidationCallback = ValidateServerCertificate;

        try
        {
            var secureSocketOptions = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

            await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }

    private MimeMessage BuildMessage(string recipientEmail, string subject, string htmlBody, string? replyTo)
    {
        var message = new MimeMessage();

        var fromAddress = new MailboxAddress(_options.DisplayName ?? _options.From, _options.From);
        message.From.Add(fromAddress);

        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;

        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        message.Body = bodyBuilder.ToMessageBody();

        return message;
    }

    private bool ValidateServerCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? certificate, System.Security.Cryptography.X509Certificates.X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        _logger.LogWarning("SMTP server certificate validation failed for host {Host} with errors {Errors}.", _options.Host, sslPolicyErrors);
        return false;
    }
}