namespace Dataportal.Services.Email;

public class MailOptions
{
    public const string SectionName = "Mail";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool UseStartTls { get; set; } = true;
}