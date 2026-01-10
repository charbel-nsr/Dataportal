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
        public async Task<IActionResult> QueryParquetAsync([FromQuery] int? id, [FromQuery] int? limit, [FromQuery] string? cursor, CancellationToken cancellationToken)
        {
            if (!id.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Dataset id is required",
                    Detail = "A dataset id must be provided.",
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

            var metadonnee = await FindMetadonneeByIdAsync(id.Value, cancellationToken);
            if (metadonnee == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = $"No dataset metadata is associated with id '{id.Value}'."
                });
            }

            if (metadonnee.IdVisibilite != VisibiliteIds.Public || !metadonnee.AutoriserApi)
            {
                return Forbid();
            }

            if (!TryResolveDatasetTarget(metadonnee, out var target, out var resolutionError))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = resolutionError ?? "Dataset table could not be resolved."
                });
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

            return await StreamParquetAsync(target, primaryKeyColumns, limit.Value, cursorValues, cancellationToken);
        }
    }
}
