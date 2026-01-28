using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Controllers.JupyterHubApi
{
    [Authorize(AuthenticationSchemes = NotebookTokenDefaults.AuthenticationScheme)]
    [Route("api/jupyterhub")]
    public class JupyterHubDataController : NotebookApi.NotebookApiBaseController
    {
        public JupyterHubDataController(ApplicationDbContext context, IOptions<NotebookApiOptions> options)
            : base(context, options)
        {
        }

        [HttpGet("normal/parquet")]
        public async Task<IActionResult> QueryParquetAsync(
            [FromQuery] string? schema,
            [FromQuery] string? table,
            [FromQuery] int? limit,
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var validationError = ValidateLimit(limit);
            if (validationError != null)
            {
                return validationError;
            }

            var (resolution, errorResult) = await TryResolveDatasetAsync(schema, table, cancellationToken);
            if (errorResult != null)
            {
                return errorResult;
            }

            var primaryKeyColumns = await GetPrimaryKeyColumnsAsync(resolution!.Target, cancellationToken);
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

            var tokenId = TryGetNotebookTokenId();
            var rateLimitContext = tokenId.HasValue ? BuildPrivateTokenRateLimitContext(tokenId.Value) : null;
            var rateLimitResult = await EnforceRateLimitAsync(rateLimitContext, cancellationToken);
            if (rateLimitResult != null)
            {
                return rateLimitResult;
            }

            var accessContext = BuildAccessContext(resolution.Metadonnee.Id);
            return await StreamParquetAsync(
                resolution.Target,
                primaryKeyColumns,
                limit!.Value,
                cursorValues,
                accessContext,
                rateLimitContext,
                cancellationToken);
        }

        [HttpGet("indexed/parquet")]
        public async Task<IActionResult> QueryIndexedParquetAsync(
            [FromQuery] string? schema,
            [FromQuery] string? table,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? indexId,
            [FromQuery] string? cursor,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            var validationError = ValidateLimit(limit);
            if (validationError != null)
            {
                return validationError;
            }

            var (resolution, errorResult) = await TryResolveDatasetAsync(schema, table, cancellationToken);
            if (errorResult != null)
            {
                return errorResult;
            }

            var primaryKeyColumns = await GetPrimaryKeyColumnsAsync(resolution!.Target, cancellationToken);
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

            var (indexMetadata, indexError) = TryGetIndexMetadata(resolution!.Metadonnee, resolution.TableType);
            if (indexError != null)
            {
                return indexError;
            }

            var timeColumnType = await GetColumnTypeAsync(resolution.Target, indexMetadata!.TimeColumn!, cancellationToken) ?? SqlDbType.DateTime2;
            object? startValue = null;
            object? endValue = null;

            if (!string.IsNullOrWhiteSpace(startDate)
                && !TryParseSqlFilterValue(startDate, timeColumnType, out startValue, out var startError))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid start date",
                    Detail = startError,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (!string.IsNullOrWhiteSpace(endDate)
                && !TryParseSqlFilterValue(endDate, timeColumnType, out endValue, out var endError))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid end date",
                    Detail = endError,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (startValue is IComparable comparableStart
                && endValue is IComparable comparableEnd
                && comparableStart.CompareTo(endValue) > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid date range",
                    Detail = "StartDate must be before EndDate.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var parameters = new List<SqlParameter>();
            var whereParts = new List<string>();

            var orderByColumns = new List<string>();

            if (!string.IsNullOrWhiteSpace(indexMetadata.IdColumn))
            {
                orderByColumns.Add(indexMetadata.IdColumn);

                if (!string.IsNullOrWhiteSpace(indexId))
                {
                    var idColumnType = await GetColumnTypeAsync(resolution.Target, indexMetadata.IdColumn, cancellationToken) ?? SqlDbType.NVarChar;
                    if (!TryParseSqlFilterValue(indexId, idColumnType, out var parsedIdValue, out var idError))
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Invalid index id",
                            Detail = idError,
                            Status = StatusCodes.Status400BadRequest,
                            Type = "https://httpstatuses.com/400"
                        });
                    }

                    parameters.Add(new SqlParameter("@indexId", idColumnType) { Value = parsedIdValue! });
                    whereParts.Add($"{EscapeColumn(indexMetadata.IdColumn)} = @indexId");
                }
            }
            else if (!string.IsNullOrWhiteSpace(indexId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Index id not supported",
                    Detail = "This dataset does not have an indexed id column.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (startValue != null)
            {
                parameters.Add(new SqlParameter("@startDate", timeColumnType) { Value = startValue });
                whereParts.Add($"{EscapeColumn(indexMetadata.TimeColumn!)} >= @startDate");
            }

            if (endValue != null)
            {
                parameters.Add(new SqlParameter("@endDate", timeColumnType) { Value = endValue });
                whereParts.Add($"{EscapeColumn(indexMetadata.TimeColumn!)} <= @endDate");
            }

            orderByColumns.Add(indexMetadata.TimeColumn!);

            var tokenId = TryGetNotebookTokenId();
            var rateLimitContext = tokenId.HasValue ? BuildPrivateTokenRateLimitContext(tokenId.Value) : null;
            var rateLimitResult = await EnforceRateLimitAsync(rateLimitContext, cancellationToken);
            if (rateLimitResult != null)
            {
                return rateLimitResult;
            }

            var accessContext = BuildAccessContext(resolution.Metadonnee.Id);
            return await StreamParquetAsync(
                resolution.Target,
                primaryKeyColumns,
                whereParts.Count == 0 ? null : string.Join(" AND ", whereParts),
                BuildOrderByClause(orderByColumns),
                limit!.Value,
                parameters,
                cursorValues,
                accessContext,
                rateLimitContext,
                cancellationToken);
        }

        [HttpGet("include/parquet")]
        public async Task<IActionResult> QueryIncludeParquetAsync(
            [FromQuery] string? schema,
            [FromQuery] string? table,
            [FromQuery] string? includeValue,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? indexId,
            [FromQuery] string? cursor,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            var validationError = ValidateLimit(limit);
            if (validationError != null)
            {
                return validationError;
            }

            var (resolution, errorResult) = await TryResolveDatasetAsync(schema, table, cancellationToken);
            if (errorResult != null)
            {
                return errorResult;
            }

            var primaryKeyColumns = await GetPrimaryKeyColumnsAsync(resolution!.Target, cancellationToken);
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

            var (indexMetadata, indexError) = TryGetIndexMetadata(resolution!.Metadonnee, resolution.TableType);
            if (indexError != null)
            {
                return indexError;
            }

            if (string.IsNullOrWhiteSpace(indexMetadata!.IncludeColumn))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Include column missing",
                    Detail = "This dataset does not expose an include column for indexed queries.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var timeColumnType = await GetColumnTypeAsync(resolution.Target, indexMetadata.TimeColumn!, cancellationToken) ?? SqlDbType.DateTime2;
            object? startValue = null;
            object? endValue = null;

            if (!string.IsNullOrWhiteSpace(startDate)
                && !TryParseSqlFilterValue(startDate, timeColumnType, out startValue, out var startError))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid start date",
                    Detail = startError,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (!string.IsNullOrWhiteSpace(endDate)
                && !TryParseSqlFilterValue(endDate, timeColumnType, out endValue, out var endError))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid end date",
                    Detail = endError,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (startValue is IComparable comparableStart
                && endValue is IComparable comparableEnd
                && comparableStart.CompareTo(endValue) > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid date range",
                    Detail = "StartDate must be before EndDate.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var parameters = new List<SqlParameter>();
            var whereParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(includeValue))
            {
                var includeColumnType = await GetColumnTypeAsync(resolution.Target, indexMetadata.IncludeColumn, cancellationToken) ?? SqlDbType.NVarChar;
                if (!TryParseSqlFilterValue(includeValue, includeColumnType, out var parsedInclude, out var includeError))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Invalid include value",
                        Detail = includeError,
                        Status = StatusCodes.Status400BadRequest,
                        Type = "https://httpstatuses.com/400"
                    });
                }

                parameters.Add(new SqlParameter("@includeValue", includeColumnType) { Value = parsedInclude! });
                whereParts.Add($"{EscapeColumn(indexMetadata.IncludeColumn)} = @includeValue");
            }

            if (!string.IsNullOrWhiteSpace(indexMetadata.IdColumn))
            {
                if (!string.IsNullOrWhiteSpace(indexId))
                {
                    var idColumnType = await GetColumnTypeAsync(resolution.Target, indexMetadata.IdColumn, cancellationToken) ?? SqlDbType.NVarChar;
                    if (!TryParseSqlFilterValue(indexId, idColumnType, out var parsedIdValue, out var idError))
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Invalid index id",
                            Detail = idError,
                            Status = StatusCodes.Status400BadRequest,
                            Type = "https://httpstatuses.com/400"
                        });
                    }

                    parameters.Add(new SqlParameter("@indexId", idColumnType) { Value = parsedIdValue! });
                    whereParts.Add($"{EscapeColumn(indexMetadata.IdColumn)} = @indexId");
                }
            }
            else if (!string.IsNullOrWhiteSpace(indexId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Index id not supported",
                    Detail = "This dataset does not have an indexed id column.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (startValue != null)
            {
                parameters.Add(new SqlParameter("@startDate", timeColumnType) { Value = startValue });
                whereParts.Add($"{EscapeColumn(indexMetadata.TimeColumn!)} >= @startDate");
            }

            if (endValue != null)
            {
                parameters.Add(new SqlParameter("@endDate", timeColumnType) { Value = endValue });
                whereParts.Add($"{EscapeColumn(indexMetadata.TimeColumn!)} <= @endDate");
            }

            var orderByColumns = new List<string>();

            if (!string.IsNullOrWhiteSpace(indexMetadata.IdColumn))
            {
                orderByColumns.Add(indexMetadata.IdColumn);
            }

            if (!string.IsNullOrWhiteSpace(indexMetadata.TimeColumn))
            {
                orderByColumns.Add(indexMetadata.TimeColumn);
            }

            orderByColumns.Add(indexMetadata.IncludeColumn);

            var tokenId = TryGetNotebookTokenId();
            var rateLimitContext = tokenId.HasValue ? BuildPrivateTokenRateLimitContext(tokenId.Value) : null;
            var rateLimitResult = await EnforceRateLimitAsync(rateLimitContext, cancellationToken);
            if (rateLimitResult != null)
            {
                return rateLimitResult;
            }

            var accessContext = BuildAccessContext(resolution.Metadonnee.Id);
            return await StreamParquetAsync(
                resolution.Target,
                primaryKeyColumns,
                whereParts.Count == 0 ? null : string.Join(" AND ", whereParts),
                BuildOrderByClause(orderByColumns),
                limit!.Value,
                parameters,
                cursorValues,
                BuildIncludeSelectColumns(indexMetadata),
                accessContext,
                rateLimitContext,
                cancellationToken);
        }

        private async Task<(DatasetResolution? Resolution, IActionResult? Error)> TryResolveDatasetAsync(
            string? schema,
            string? table,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Schema is required",
                    Detail = "A schema must be provided (donnees, donnees_event_logs, donnees_contexte_environnemental).",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (!TryParseTableType(schema, out var parsedTableType))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Invalid schema",
                    Detail = "Schema must be one of: donnees, donnees_event_logs, donnees_contexte_environnemental.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (string.IsNullOrWhiteSpace(table))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Table name is required",
                    Detail = "A table name must be provided.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (!IsValidTableName(table))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Invalid table name",
                    Detail = "Table name contains unsupported characters.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            var metadonnee = await FindMetadonneeByTableNameAsync(parsedTableType, table, cancellationToken);
            if (metadonnee == null)
            {
                return (null, NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = $"No dataset metadata is associated with schema '{schema}' and table '{table}'."
                }));
            }

            if (!metadonnee.AutoriserApi)
            {
                return (null, Forbid());
            }

            if (!HttpContextUserHelper.CanCurrentUserAccessMetadonnee(HttpContext, Context, metadonnee, out var requiresAuthentication))
            {
                return (null, requiresAuthentication
                    ? Challenge(NotebookTokenDefaults.AuthenticationScheme)
                    : Forbid());
            }

            var roleId = HttpContextUserHelper.GetCurrentUserRole(HttpContext, Context);
            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
            var isAdmin = roleId == RoleIds.Administrateur;
            var isEditorOwner = roleId == RoleIds.Editeur && userId.HasValue && metadonnee.IdUtilisateur == userId.Value;

            if (!isAdmin && !isEditorOwner)
            {
                return (null, Forbid());
            }

            if (!TryResolveDatasetTarget(metadonnee, parsedTableType, out var target, out var resolutionError))
            {
                return (null, NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = resolutionError ?? "Dataset table could not be resolved."
                }));
            }

            return (new DatasetResolution(metadonnee, parsedTableType, target), null);
        }

        private (IndexMetadata? Metadata, IActionResult? Error) TryGetIndexMetadata(Metadonnee metadonnee, NotebookApi.NotebookApiBaseController.NotebookApiTableType tableType)
        {
            var metadata = tableType switch
            {
                NotebookApi.NotebookApiBaseController.NotebookApiTableType.Donnees => metadonnee.Donnees == null
                    ? null
                    : new IndexMetadata(metadonnee.Donnees.IndexEnabled, metadonnee.Donnees.IndexTimeColumn, metadonnee.Donnees.IndexIdColumn, metadonnee.Donnees.IndexIncludeColumn),
                NotebookApi.NotebookApiBaseController.NotebookApiTableType.EventLogs => metadonnee.DonneesEventLogs == null
                    ? null
                    : new IndexMetadata(metadonnee.DonneesEventLogs.IndexEnabled, metadonnee.DonneesEventLogs.IndexTimeColumn, metadonnee.DonneesEventLogs.IndexIdColumn, metadonnee.DonneesEventLogs.IndexIncludeColumn),
                NotebookApi.NotebookApiBaseController.NotebookApiTableType.ContexteEnvironnemental => metadonnee.DonneesContexteEnvironnemental == null
                    ? null
                    : new IndexMetadata(
                        metadonnee.DonneesContexteEnvironnemental.IndexEnabled,
                        metadonnee.DonneesContexteEnvironnemental.IndexTimeColumn,
                        metadonnee.DonneesContexteEnvironnemental.IndexIdColumn,
                        metadonnee.DonneesContexteEnvironnemental.IndexIncludeColumn),
                _ => null
            };

            if (metadata == null)
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Index metadata missing",
                    Detail = "Index metadata could not be resolved for this dataset.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (!metadata.Enabled)
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Index not enabled",
                    Detail = "Index-based queries are not enabled for this dataset.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (string.IsNullOrWhiteSpace(metadata.TimeColumn))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Index time column missing",
                    Detail = "Index-based queries require a time column.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (!IsValidColumnName(metadata.TimeColumn))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Invalid index time column",
                    Detail = "Index time column contains unsupported characters.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (!string.IsNullOrWhiteSpace(metadata.IdColumn) && !IsValidColumnName(metadata.IdColumn))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Invalid index id column",
                    Detail = "Index id column contains unsupported characters.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            if (!string.IsNullOrWhiteSpace(metadata.IncludeColumn) && !IsValidColumnName(metadata.IncludeColumn))
            {
                return (null, BadRequest(new ProblemDetails
                {
                    Title = "Invalid index include column",
                    Detail = "Index include column contains unsupported characters.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                }));
            }

            return (metadata, null);
        }

        private IActionResult? ValidateLimit(int? limit)
        {
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

            return null;
        }

        private static string BuildOrderByClause(IEnumerable<string> columns)
        {
            return string.Join(", ", columns.Where(column => !string.IsNullOrWhiteSpace(column))
                .Distinct()
                .Select(column => $"{EscapeColumn(column)} ASC"));
        }

        private static bool TryParseSqlFilterValue(string rawValue, SqlDbType dbType, out object? parsedValue, out string? error)
        {
            error = null;
            parsedValue = null;

            switch (dbType)
            {
                case SqlDbType.BigInt:
                    if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                    {
                        parsedValue = longValue;
                        return true;
                    }
                    error = "Value must be a 64-bit integer.";
                    return false;
                case SqlDbType.Int:
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        parsedValue = intValue;
                        return true;
                    }
                    error = "Value must be an integer.";
                    return false;
                case SqlDbType.SmallInt:
                    if (short.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortValue))
                    {
                        parsedValue = shortValue;
                        return true;
                    }
                    error = "Value must be a 16-bit integer.";
                    return false;
                case SqlDbType.TinyInt:
                    if (byte.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteValue))
                    {
                        parsedValue = byteValue;
                        return true;
                    }
                    error = "Value must be an 8-bit integer.";
                    return false;
                case SqlDbType.Bit:
                    if (bool.TryParse(rawValue, out var boolValue))
                    {
                        parsedValue = boolValue;
                        return true;
                    }

                    if (rawValue == "0" || rawValue == "1")
                    {
                        parsedValue = rawValue == "1";
                        return true;
                    }

                    error = "Value must be a boolean.";
                    return false;
                case SqlDbType.Decimal:
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        parsedValue = decimalValue;
                        return true;
                    }
                    error = "Value must be a decimal number.";
                    return false;
                case SqlDbType.Float:
                    if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                    {
                        parsedValue = doubleValue;
                        return true;
                    }
                    error = "Value must be a floating-point number.";
                    return false;
                case SqlDbType.Real:
                    if (float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        parsedValue = floatValue;
                        return true;
                    }
                    error = "Value must be a floating-point number.";
                    return false;
                case SqlDbType.Date:
                case SqlDbType.DateTime:
                case SqlDbType.DateTime2:
                case SqlDbType.SmallDateTime:
                    if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateValue))
                    {
                        parsedValue = dateValue;
                        return true;
                    }
                    error = "Value must be a valid date/time.";
                    return false;
                case SqlDbType.DateTimeOffset:
                    if (DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateOffset))
                    {
                        parsedValue = dateOffset;
                        return true;
                    }
                    error = "Value must be a valid date/time with offset.";
                    return false;
                case SqlDbType.Time:
                    if (TimeSpan.TryParse(rawValue, CultureInfo.InvariantCulture, out var timeValue))
                    {
                        parsedValue = timeValue;
                        return true;
                    }
                    error = "Value must be a valid time span.";
                    return false;
                case SqlDbType.UniqueIdentifier:
                    if (Guid.TryParse(rawValue, out var guidValue))
                    {
                        parsedValue = guidValue;
                        return true;
                    }
                    error = "Value must be a valid GUID.";
                    return false;
                default:
                    parsedValue = rawValue;
                    return true;
            }
        }

        private static IReadOnlyList<string> BuildIncludeSelectColumns(IndexMetadata metadata)
        {
            var columns = new List<string>();

            if (!string.IsNullOrWhiteSpace(metadata.IdColumn))
            {
                columns.Add(metadata.IdColumn);
            }

            if (!string.IsNullOrWhiteSpace(metadata.TimeColumn))
            {
                columns.Add(metadata.TimeColumn);
            }

            if (!string.IsNullOrWhiteSpace(metadata.IncludeColumn))
            {
                columns.Add(metadata.IncludeColumn);
            }

            return columns;
        }

        private sealed record DatasetResolution(Metadonnee Metadonnee, NotebookApi.NotebookApiBaseController.NotebookApiTableType TableType, TableImportTarget Target);

        private sealed record IndexMetadata(bool Enabled, string? TimeColumn, string? IdColumn, string? IncludeColumn);
    }
}