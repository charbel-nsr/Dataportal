using Dataportal.Context;
using Dataportal.Models;
using Dataportal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Controllers.NotebookApi
{
    [ApiController]
    public abstract class NotebookApiBaseController : ControllerBase
    {
        protected NotebookApiBaseController(ApplicationDbContext context)
        {
            Context = context;
        }

        protected ApplicationDbContext Context { get; }

        protected IActionResult? EnsureAllowedSchema(string? schema)
        {
            if (NotebookSchemaAllowlist.IsAllowed(schema))
            {
                return null;
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Schema not allowed",
                Detail = $"Schema '{schema}' is not allowed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }

        protected static string BuildQualifiedTable(string schema, string table)
        {
            var safeSchema = schema.Replace("]", "]]", StringComparison.Ordinal);
            var safeTable = table.Replace("]", "]]", StringComparison.Ordinal);
            return $"[DataPortal].[{safeSchema}].[{safeTable}]";
        }

        protected IActionResult? ValidateTableName(string? table, out string normalizedTable)
        {
            normalizedTable = table?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedTable))
            {
                return null;
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Table name is required",
                Detail = "A table name must be provided.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }

        protected async Task<Metadonnee?> FindMetadonneeForTableAsync(string normalizedTable, CancellationToken cancellationToken)
        {
            var normalizedLookup = normalizedTable.ToUpperInvariant();

            return await Context.Metadonnee
                .AsNoTracking()
                .Include(m => m.Utilisateur)
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m =>
                        (m.Donnees != null && m.Donnees.NomDeLaTable != null && m.Donnees.NomDeLaTable.ToUpper() == normalizedLookup) ||
                        (m.DonneesEventLogs != null && m.DonneesEventLogs.NomDeLaTable != null && m.DonneesEventLogs.NomDeLaTable.ToUpper() == normalizedLookup) ||
                        (m.DonneesContexteEnvironnemental != null && m.DonneesContexteEnvironnemental.NomDeLaTable != null && m.DonneesContexteEnvironnemental.NomDeLaTable.ToUpper() == normalizedLookup),
                    cancellationToken);
        }
    }
}