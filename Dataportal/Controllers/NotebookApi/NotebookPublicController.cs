using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Controllers.NotebookApi
{
    [Route("api/notebook/public")]
    public class NotebookPublicController : NotebookApiBaseController
    {
        public NotebookPublicController(ApplicationDbContext context, IOptions<NotebookApiOptions> options)
            : base(context, options)
        {
        }

        [HttpGet("donnees/parquet")]
        public async Task<IActionResult> QueryParquetAsync(
            [FromQuery] string? schema,
            [FromQuery] string? table,
            [FromQuery] int? limit,
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Schema is required",
                    Detail = "A schema must be provided (donnees, donnees_event_logs, donnees_contexte_environnemental).",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (!TryParseTableType(schema, out var parsedTableType))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid schema",
                    Detail = "Schema must be one of: donnees, donnees_event_logs, donnees_contexte_environnemental.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (string.IsNullOrWhiteSpace(table))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Table name is required",
                    Detail = "A table name must be provided.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (!IsValidTableName(table))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid table name",
                    Detail = "Table name contains unsupported characters.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (!limit.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Limit is required",
                    Detail = "A limit must be provided for cursor paging.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (limit.Value < 1 || limit.Value > Options.MaxRowsPerPage)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Limit out of range",
                    Detail = $"Limit must be between 1 and {Options.MaxRowsPerPage}.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var metadonnee = await FindMetadonneeByTableNameAsync(parsedTableType, table, cancellationToken);
            if (metadonnee == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = $"No dataset metadata is associated with schema '{schema}' and table '{table}'."
                });
            }

            if (metadonnee.IdVisibilite != VisibiliteIds.Public || !metadonnee.AutoriserApi)
            {
                return Forbid();
            }

            if (metadonnee.TraitementEnCours == true)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Table locked",
                    Detail = "Dataset operations are unavailable while a replacement is in progress.",
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://httpstatuses.com/409"
                });
            }

            if (!TryResolveDatasetTarget(metadonnee, parsedTableType, out var target, out var resolutionError))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = resolutionError ?? "Dataset table could not be resolved."
                });
            }

            var rateLimitContext = BuildPublicRateLimitContext();
            var rateLimitResult = await EnforceRateLimitAsync(rateLimitContext, cancellationToken);
            if (rateLimitResult != null)
            {
                return rateLimitResult;
            }

            var primaryKeyColumns = await GetPrimaryKeyColumnsAsync(target, cancellationToken);
            if (primaryKeyColumns.Count == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Primary key required",
                    Detail = "Notebook API requires a primary key for cursor paging.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            object?[]? cursorValues = null;
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                try
                {
                    cursorValues = DecodeCursor(cursor, primaryKeyColumns);
                }
                catch (Exception ex)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Invalid cursor",
                        Detail = ex.Message,
                        Status = StatusCodes.Status400BadRequest,
                        Type = "https://httpstatuses.com/400"
                    });
                }
            }

            var accessContext = BuildAccessContext(metadonnee.Id);
            return await StreamParquetAsync(target, primaryKeyColumns, limit.Value, cursorValues, accessContext, rateLimitContext, cancellationToken);
        }
    }
}
