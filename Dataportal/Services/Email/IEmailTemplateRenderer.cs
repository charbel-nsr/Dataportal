namespace Dataportal.Services.Email;

public interface IEmailTemplateRenderer
{
    string RenderHtml(string subject, string bodyHtml);
}