using Dataportal.Context;
using Dataportal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

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
    }
}