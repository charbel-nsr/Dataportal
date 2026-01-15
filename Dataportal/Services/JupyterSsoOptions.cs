namespace Dataportal.Services
{
    /// <summary>
    /// Options for internal JupyterHub SSO token issuance (separate from NotebookApi).
    /// </summary>
    public class JupyterSsoOptions
{
    public const string SectionName = "JupyterSso";

    public string SigningKey { get; set; } = string.Empty;
}
}