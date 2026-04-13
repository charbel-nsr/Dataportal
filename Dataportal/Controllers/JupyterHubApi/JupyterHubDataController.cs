using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
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
        private readonly NotebookReplaceSessionService _replaceSessionService;

        public JupyterHubDataController(ApplicationDbContext context, IOptions<NotebookApiOptions> options, NotebookReplaceSessionService replaceSessionService)
            : base(context, options)
        {
            _replaceSessionService = replaceSessionService ?? throw new ArgumentNullException(nameof(replaceSessionService));
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

        [HttpPost("/api/tables/{schema}/{table}/replace/start")]
        public async Task<IActionResult> StartReplaceSessionAsync(
            [FromRoute] string schema,
            [FromRoute] string table,
            CancellationToken cancellationToken)
        {
            await AbortExpiredReplaceSessionsAsync(cancellationToken);
            var (resolution, errorResult) = await TryResolveDatasetAsync(schema, table, cancellationToken);
            if (errorResult != null)
            {
                return errorResult;
            }

            if (resolution!.Metadonnee.TraitementEnCours == true)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Table locked",
                    Detail = "A replace session is already in progress for this dataset.",
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://httpstatuses.com/409"
                });
            }

            var jobId = Guid.NewGuid();
            var stagingTableName = BuildStagingTableName(resolution.Target.TableName, jobId);

            if (!IsValidTableName(stagingTableName))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid staging table name",
                    Detail = "The generated staging table name is not valid.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            await using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);

            var locked = await TryLockMetadonneeAsync(resolution.Metadonnee.Id, cancellationToken);
            if (!locked)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Table locked",
                    Detail = "A replace session is already in progress for this dataset.",
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://httpstatuses.com/409"
                });
            }

            var session = new NotebookReplaceSession
            {
                Id = jobId,
                IdMetadonnee = resolution.Metadonnee.Id,
                Schema = resolution.Target.Schema,
                TableName = resolution.Target.TableName,
                StagingTableName = stagingTableName,
                Status = NotebookReplaceStatus.Started,
                IdUtilisateur = HttpContextUserHelper.TryGetCurrentUserId(User)
            };

            var connection = (SqlConnection)Context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var sqlTransaction = Context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;
            var stagingTarget = new TableImportTarget(resolution.Target.Schema, stagingTableName);

            try
            {
                await CreateStagingTableAsync(resolution.Target, stagingTarget, connection, sqlTransaction, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Unable to create staging table",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            Context.NotebookReplaceSessions.Add(session);
            await Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Created($"/api/jupyterhub/replace/{jobId}", new ReplaceStartResponse(jobId, stagingTableName));
        }

        [HttpPost("replace/{jobId:guid}/chunk")]
        public async Task<IActionResult> UploadReplaceChunkAsync(
            [FromRoute] Guid jobId,
            [FromForm] IFormFile? file,
            CancellationToken cancellationToken)
        {
            await AbortExpiredReplaceSessionsAsync(cancellationToken);
            var session = await LoadReplaceSessionAsync(jobId, cancellationToken);
            if (session == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Replace session not found",
                    Detail = "No replace session matches the provided job id.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            var accessError = EnsureReplaceSessionAccess(session);
            if (accessError != null)
            {
                return accessError;
            }

            if (session.Status == NotebookReplaceStatus.Committed || session.Status == NotebookReplaceStatus.Aborted || session.Status == NotebookReplaceStatus.Pushed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Replace session closed",
                    Detail = "This replace session can no longer accept uploads.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Chunk required",
                    Detail = "A Parquet chunk must be provided.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var stagingTarget = new TableImportTarget(session.Schema, session.StagingTableName);
            var connectionString = GetConnectionStringOrThrow();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(stagingTarget, connection, null, cancellationToken))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Staging table missing",
                    Detail = "The staging table for this replace session could not be found.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            var columns = await GetTableColumnsAsync(stagingTarget, connection, null, cancellationToken);
            var dataColumns = columns.Where(c => !c.IsIdentity).ToList();
            if (dataColumns.Count == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Table schema missing",
                    Detail = "The staging table does not contain any data columns.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var columnDefinitions = dataColumns.Select(BuildTabularColumnDefinition).ToList();

            await using var stream = file.OpenReadStream();
            using var reader = await ParquetReader.CreateAsync(stream, cancellationToken: cancellationToken);
            var headerInfo = ReadNormalizedParquetHeader(reader);
            if (headerInfo == null || headerInfo.Value.Headers.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Empty parquet",
                    Detail = "The Parquet chunk does not contain any columns.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            try
            {
                EnsureUniqueHeaders(headerInfo.Value.Headers);
                ValidateHeadersAgainstColumns(headerInfo.Value.Headers, columnDefinitions, "Parquet");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid parquet schema",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            using var bulkCopy = CreateBulkCopy(connection, stagingTarget, columnDefinitions, ReplaceBulkBatchSize);
            using var buffer = CreateBufferTable(columnDefinitions);
            var errors = new List<TabularImportError>();

            var rowsCopied = await BulkCopyParquetRowsAsync(
                bulkCopy,
                buffer,
                columnDefinitions,
                reader,
                headerInfo.Value.Fields,
                errors,
                file.FileName,
                ReplaceBulkBatchSize,
                cancellationToken);

            session.Status = NotebookReplaceStatus.Uploading;
            session.UpdatedAtUtc = DateTime.UtcNow;
            await Context.SaveChangesAsync(cancellationToken);

            return Ok(new ReplaceChunkResponse(rowsCopied, errors));
        }

        [HttpPost("replace/{jobId:guid}/commit")]
        public async Task<IActionResult> CommitReplaceSessionAsync(
            [FromRoute] Guid jobId,
            CancellationToken cancellationToken)
        {
            await AbortExpiredReplaceSessionsAsync(cancellationToken);

            var session = await LoadReplaceSessionAsync(jobId, cancellationToken);
            if (session == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Replace session not found",
                    Detail = "No replace session matches the provided job id.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            var accessError = EnsureReplaceSessionAccess(session);
            if (accessError != null)
            {
                return accessError;
            }

            if (session.Status == NotebookReplaceStatus.Committed || session.Status == NotebookReplaceStatus.Aborted || session.Status == NotebookReplaceStatus.Pushed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Replace session closed",
                    Detail = "This replace session has already been finalized.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var sourceTarget = new TableImportTarget(session.Schema, session.TableName);
            var stagingTarget = new TableImportTarget(session.Schema, session.StagingTableName);
            var oldTableName = BuildOldTableName(session.TableName, session.Id);
            var oldTarget = new TableImportTarget(session.Schema, oldTableName);

            await using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
            var connection = (SqlConnection)Context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var sqlTransaction = Context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            if (!await TableExistsAsync(stagingTarget, connection, sqlTransaction, cancellationToken))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Staging table missing",
                    Detail = "The staging table for this replace session could not be found.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            if (!await TableHasRowsAsync(stagingTarget, connection, sqlTransaction, cancellationToken))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "No data uploaded",
                    Detail = "The staging table does not contain any rows.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            if (await TableExistsAsync(oldTarget, connection, sqlTransaction, cancellationToken))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Old table already exists",
                    Detail = "The old table name is already in use.",
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://httpstatuses.com/409"
                });
            }

            await RenameTableAsync(sourceTarget, oldTableName, connection, sqlTransaction, cancellationToken);
            await RenameTableAsync(stagingTarget, session.TableName, connection, sqlTransaction, cancellationToken);

            session.Status = NotebookReplaceStatus.Committed;
            session.OldTableName = oldTableName;
            session.CommittedAtUtc = DateTime.UtcNow;
            session.UpdatedAtUtc = session.CommittedAtUtc;

            await Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new ReplaceCommitResponse(oldTableName, "Replace committed. Review the dataset stats on the website before pushing."));
        }

        [HttpPost("replace/{jobId:guid}/abort")]
        public async Task<IActionResult> AbortReplaceSessionAsync(
            [FromRoute] Guid jobId,
            CancellationToken cancellationToken)
        {
            await AbortExpiredReplaceSessionsAsync(cancellationToken);

            var session = await LoadReplaceSessionAsync(jobId, cancellationToken);
            if (session == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Replace session not found",
                    Detail = "No replace session matches the provided job id.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            var accessError = EnsureReplaceSessionAccess(session);
            if (accessError != null)
            {
                return accessError;
            }

            if (session.Status == NotebookReplaceStatus.Aborted || session.Status == NotebookReplaceStatus.Pushed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Replace session closed",
                    Detail = "This replace session has already been finalized.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var sourceTarget = new TableImportTarget(session.Schema, session.TableName);
            var stagingTarget = new TableImportTarget(session.Schema, session.StagingTableName);
            var oldTarget = session.OldTableName == null
                ? null
                : new TableImportTarget(session.Schema, session.OldTableName);

            await using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
            var connection = (SqlConnection)Context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var sqlTransaction = Context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            if (session.Status == NotebookReplaceStatus.Committed && oldTarget != null)
            {
                if (!await TableExistsAsync(oldTarget, connection, sqlTransaction, cancellationToken))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Old table missing",
                        Detail = "The previous version of the table could not be found.",
                        Status = StatusCodes.Status404NotFound,
                        Type = "https://httpstatuses.com/404"
                    });
                }

                if (await TableExistsAsync(sourceTarget, connection, sqlTransaction, cancellationToken))
                {
                    await DropTableAsync(sourceTarget, connection, sqlTransaction, cancellationToken);
                }

                await RenameTableAsync(oldTarget, session.TableName, connection, sqlTransaction, cancellationToken);
            }
            else if (await TableExistsAsync(stagingTarget, connection, sqlTransaction, cancellationToken))
            {
                await DropTableAsync(stagingTarget, connection, sqlTransaction, cancellationToken);
            }

            await UnlockMetadonneeAsync(session.IdMetadonnee, cancellationToken);

            session.Status = NotebookReplaceStatus.Aborted;
            session.CompletedAtUtc = DateTime.UtcNow;
            session.UpdatedAtUtc = session.CompletedAtUtc;

            await Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new ReplaceCompletionResponse("Replace session aborted and the dataset has been unlocked."));
        }

        [HttpPost("replace/{jobId:guid}/push")]
        public async Task<IActionResult> PushReplaceSessionAsync(
            [FromRoute] Guid jobId,
            CancellationToken cancellationToken)
        {
            await AbortExpiredReplaceSessionsAsync(cancellationToken);

            var session = await LoadReplaceSessionAsync(jobId, cancellationToken);
            if (session == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Replace session not found",
                    Detail = "No replace session matches the provided job id.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            var accessError = EnsureReplaceSessionAccess(session);
            if (accessError != null)
            {
                return accessError;
            }

            if (session.Status != NotebookReplaceStatus.Committed || string.IsNullOrWhiteSpace(session.OldTableName))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Replace session not ready",
                    Detail = "This replace session must be committed before pushing.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var oldTarget = new TableImportTarget(session.Schema, session.OldTableName);

            await using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
            var connection = (SqlConnection)Context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var sqlTransaction = Context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            if (await TableExistsAsync(oldTarget, connection, sqlTransaction, cancellationToken))
            {
                await DropTableAsync(oldTarget, connection, sqlTransaction, cancellationToken);
            }

            await ClearIndexMetadataAsync(session, cancellationToken);

            await UnlockMetadonneeAsync(session.IdMetadonnee, cancellationToken);

            session.Status = NotebookReplaceStatus.Pushed;
            session.CompletedAtUtc = DateTime.UtcNow;
            session.UpdatedAtUtc = session.CompletedAtUtc;

            await Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new ReplaceCompletionResponse("Replace session pushed. The dataset is unlocked."));
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

            if (metadonnee.TraitementEnCours == true)
            {
                return (null, Conflict(new ProblemDetails
                {
                    Title = "Table locked",
                    Detail = "Dataset operations are unavailable while a replacement is in progress.",
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://httpstatuses.com/409"
                }));
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

        private const int ReplaceBulkBatchSize = 5000;
        private const int MaxTableNameLength = 128;

        private static string BuildStagingTableName(string baseTableName, Guid jobId)
        {
            return BuildReplaceTableName(baseTableName, "staging", jobId);
        }

        private static string BuildOldTableName(string baseTableName, Guid jobId)
        {
            return BuildReplaceTableName(baseTableName, "old", jobId);
        }

        private static string BuildReplaceTableName(string baseTableName, string label, Guid jobId)
        {
            var suffix = $"__{label}__{jobId:N}";
            var maxBaseLength = MaxTableNameLength - suffix.Length;
            if (maxBaseLength <= 0)
            {
                throw new InvalidOperationException("The replace table name is too long.");
            }

            var trimmedBase = baseTableName.Length > maxBaseLength
                ? baseTableName[..maxBaseLength]
                : baseTableName;

            return $"{trimmedBase}{suffix}";
        }

        private async Task ClearIndexMetadataAsync(NotebookReplaceSession session, CancellationToken cancellationToken)
        {
            if (!TryResolveReplaceTableType(session.Schema, out var tableType))
            {
                return;
            }

            var metadonnee = await Context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m => m.Id == session.IdMetadonnee, cancellationToken);
            if (metadonnee == null)
            {
                return;
            }

            switch (tableType)
            {
                case NotebookApi.NotebookApiBaseController.NotebookApiTableType.Donnees when metadonnee.Donnees != null:
                    ClearIndexMetadata(metadonnee.Donnees);
                    break;
                case NotebookApi.NotebookApiBaseController.NotebookApiTableType.EventLogs when metadonnee.DonneesEventLogs != null:
                    ClearIndexMetadata(metadonnee.DonneesEventLogs);
                    break;
                case NotebookApi.NotebookApiBaseController.NotebookApiTableType.ContexteEnvironnemental when metadonnee.DonneesContexteEnvironnemental != null:
                    ClearIndexMetadata(metadonnee.DonneesContexteEnvironnemental);
                    break;
            }
        }

        private static bool TryResolveReplaceTableType(
            string? schema,
            out NotebookApi.NotebookApiBaseController.NotebookApiTableType tableType)
        {
            tableType = default;
            if (string.IsNullOrWhiteSpace(schema))
            {
                return false;
            }

            if (schema.Equals(TableImportSchemas.Donnees, StringComparison.OrdinalIgnoreCase))
            {
                tableType = NotebookApi.NotebookApiBaseController.NotebookApiTableType.Donnees;
                return true;
            }

            if (schema.Equals(TableImportSchemas.DonneesEventLogs, StringComparison.OrdinalIgnoreCase))
            {
                tableType = NotebookApi.NotebookApiBaseController.NotebookApiTableType.EventLogs;
                return true;
            }

            if (schema.Equals(TableImportSchemas.DonneesContexteEnvironnemental, StringComparison.OrdinalIgnoreCase))
            {
                tableType = NotebookApi.NotebookApiBaseController.NotebookApiTableType.ContexteEnvironnemental;
                return true;
            }

            return false;
        }

        private static void ClearIndexMetadata(Models.Donnees donnees)
        {
            donnees.IndexEnabled = false;
            donnees.IndexTimeColumn = null;
            donnees.IndexIdColumn = null;
            donnees.IndexIncludeColumn = null;
            donnees.IndexType = null;
            donnees.IndexName = null;
            donnees.IndexStatus = null;
            donnees.IndexError = null;
        }

        private static void ClearIndexMetadata(Models.DonneesEventLogs donneesEventLogs)
        {
            donneesEventLogs.IndexEnabled = false;
            donneesEventLogs.IndexTimeColumn = null;
            donneesEventLogs.IndexIdColumn = null;
            donneesEventLogs.IndexIncludeColumn = null;
            donneesEventLogs.IndexType = null;
            donneesEventLogs.IndexName = null;
            donneesEventLogs.IndexStatus = null;
            donneesEventLogs.IndexError = null;
        }

        private static void ClearIndexMetadata(Models.DonneesContexteEnvironnemental donneesContexteEnvironnemental)
        {
            donneesContexteEnvironnemental.IndexEnabled = false;
            donneesContexteEnvironnemental.IndexTimeColumn = null;
            donneesContexteEnvironnemental.IndexIdColumn = null;
            donneesContexteEnvironnemental.IndexIncludeColumn = null;
            donneesContexteEnvironnemental.IndexType = null;
            donneesContexteEnvironnemental.IndexName = null;
            donneesContexteEnvironnemental.IndexStatus = null;
            donneesContexteEnvironnemental.IndexError = null;
        }

        private async Task<NotebookReplaceSession?> LoadReplaceSessionAsync(Guid jobId, CancellationToken cancellationToken)
        {
            return await Context.NotebookReplaceSessions
                .Include(s => s.Metadonnee)
                .FirstOrDefaultAsync(s => s.Id == jobId, cancellationToken);
        }

        private IActionResult? EnsureReplaceSessionAccess(NotebookReplaceSession session)
        {
            if (session.Metadonnee == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = "The dataset metadata could not be resolved.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://httpstatuses.com/404"
                });
            }

            var roleId = HttpContextUserHelper.GetCurrentUserRole(HttpContext, Context);
            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
            var isAdmin = roleId == RoleIds.Administrateur;
            var isEditorOwner = roleId == RoleIds.Editeur && userId.HasValue && session.Metadonnee.IdUtilisateur == userId.Value;

            if (!isAdmin && !isEditorOwner)
            {
                return Forbid();
            }

            return null;
        }

        private async Task<bool> TryLockMetadonneeAsync(int metadonneeId, CancellationToken cancellationToken)
        {
            var affected = await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Metadonnee SET TraitementEnCours = 1 WHERE Id = {metadonneeId} AND (TraitementEnCours IS NULL OR TraitementEnCours = 0)",
                cancellationToken);

            return affected > 0;
        }

        private async Task UnlockMetadonneeAsync(int metadonneeId, CancellationToken cancellationToken)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Metadonnee SET TraitementEnCours = 0 WHERE Id = {metadonneeId}",
                cancellationToken);
        }

        private async Task AbortExpiredReplaceSessionsAsync(CancellationToken cancellationToken)
        {
            var cutoffUtc = DateTime.UtcNow.AddHours(-1);
            await _replaceSessionService.AbortExpiredSessionsAsync(cutoffUtc, cancellationToken);
        }

        private string GetConnectionStringOrThrow()
        {
            return Context.Database.GetConnectionString()
                   ?? throw new InvalidOperationException("The database connection string could not be found.");
        }

        private static async Task<bool> TableExistsAsync(TableImportTarget target, SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT 1
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @schema AND t.name = @table;";

            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = target.Schema });
            cmd.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = target.TableName });
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result != null;
        }

        private static async Task<bool> TableHasRowsAsync(TableImportTarget target, SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
        {
            var sql = $"SELECT TOP 1 1 FROM {target.SchemaQualifiedName};";
            using var cmd = new SqlCommand(sql, connection, transaction);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result != null;
        }

        private static async Task DropTableAsync(TableImportTarget target, SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
        {
            var sql = $"DROP TABLE {target.SchemaQualifiedName};";
            using var cmd = new SqlCommand(sql, connection, transaction);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task RenameTableAsync(TableImportTarget target, string newTableName, SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
        {
            using var cmd = new SqlCommand("EXEC sp_rename @qualifiedName, @newName;", connection, transaction);
            cmd.Parameters.Add(new SqlParameter("@qualifiedName", SqlDbType.NVarChar, 260) { Value = target.SchemaQualifiedName });
            cmd.Parameters.Add(new SqlParameter("@newName", SqlDbType.NVarChar, 128) { Value = newTableName });
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task CreateStagingTableAsync(
            TableImportTarget sourceTarget,
            TableImportTarget stagingTarget,
            SqlConnection connection,
            SqlTransaction? transaction,
            CancellationToken cancellationToken)
        {
            if (await TableExistsAsync(stagingTarget, connection, transaction, cancellationToken))
            {
                throw new InvalidOperationException("The staging table already exists.");
            }

            var columns = await GetTableColumnsAsync(sourceTarget, connection, transaction, cancellationToken);
            if (columns.Count == 0)
            {
                throw new InvalidOperationException("The source table schema could not be resolved.");
            }

            var columnDefinitions = columns.Select(BuildColumnDefinition).ToList();
            var sql = $"CREATE TABLE {stagingTarget.SchemaQualifiedName} ({string.Join(", ", columnDefinitions)});";

            using var cmd = new SqlCommand(sql, connection, transaction);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<List<ColumnMetadata>> GetTableColumnsAsync(
            TableImportTarget target,
            SqlConnection connection,
            SqlTransaction? transaction,
            CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT c.name,
       t.name AS type_name,
       c.max_length,
       c.precision,
       c.scale,
       c.is_nullable,
       c.is_identity,
       ic.seed_value,
       ic.increment_value
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.identity_columns ic ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE c.object_id = OBJECT_ID(@objectId)
ORDER BY c.column_id;";

            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.Add(new SqlParameter("@objectId", SqlDbType.NVarChar, 260) { Value = target.SchemaQualifiedName });

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var columns = new List<ColumnMetadata>();
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new ColumnMetadata(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt16(2),
                    reader.GetByte(3),
                    reader.GetByte(4),
                    reader.GetBoolean(5),
                    reader.GetBoolean(6),
                    ReadOptionalDecimal(reader, 7),
                    ReadOptionalDecimal(reader, 8)));
            }

            return columns;
        }

        private static decimal? ReadOptionalDecimal(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static string BuildColumnDefinition(ColumnMetadata column)
        {
            var safeName = column.Name.Replace("]", "]]", StringComparison.Ordinal);
            var sqlType = BuildSqlType(column);
            var nullability = column.IsNullable ? "NULL" : "NOT NULL";
            var identity = column.IsIdentity
                ? $" IDENTITY({column.IdentitySeed ?? 1}, {column.IdentityIncrement ?? 1})"
                : string.Empty;

            return $"[{safeName}] {sqlType}{identity} {nullability}";
        }

        private static string BuildSqlType(ColumnMetadata column)
        {
            var typeName = column.TypeName;
            var lower = typeName.ToLowerInvariant();

            switch (lower)
            {
                case "nvarchar":
                case "nchar":
                case "varchar":
                case "char":
                case "varbinary":
                case "binary":
                    if (column.MaxLength == -1)
                    {
                        return $"{typeName}(MAX)";
                    }

                    var length = column.MaxLength;
                    if (lower is "nvarchar" or "nchar")
                    {
                        length /= 2;
                    }

                    return $"{typeName}({length})";
                case "decimal":
                case "numeric":
                    return $"{typeName}({column.Precision},{column.Scale})";
                case "datetime2":
                case "datetimeoffset":
                case "time":
                    return $"{typeName}({column.Scale})";
                default:
                    return typeName;
            }
        }

        private static TabularColumnDefinition BuildTabularColumnDefinition(ColumnMetadata column)
        {
            var typeName = column.TypeName.ToLowerInvariant();
            var columnType = typeName switch
            {
                "bit" => TabularColumnType.Bit,
                "int" => TabularColumnType.Int,
                "smallint" => TabularColumnType.Int,
                "tinyint" => TabularColumnType.Int,
                "bigint" => TabularColumnType.BigInt,
                "decimal" => TabularColumnType.Decimal,
                "numeric" => TabularColumnType.Decimal,
                "money" => TabularColumnType.Decimal,
                "smallmoney" => TabularColumnType.Decimal,
                "float" => TabularColumnType.Float,
                "real" => TabularColumnType.Float,
                "date" => TabularColumnType.DateTime2,
                "datetime" => TabularColumnType.DateTime2,
                "datetime2" => TabularColumnType.DateTime2,
                "smalldatetime" => TabularColumnType.DateTime2,
                "datetimeoffset" => TabularColumnType.DateTime2,
                "time" => TabularColumnType.DateTime2,
                _ => TabularColumnType.NVarChar
            };

            int? maxLength = null;
            if (columnType == TabularColumnType.NVarChar && column.MaxLength > 0)
            {
                if (column.MaxLength == -1)
                {
                    maxLength = null;
                }
                else if (typeName is "nvarchar" or "nchar")
                {
                    maxLength = column.MaxLength / 2;
                }
                else
                {
                    maxLength = column.MaxLength;
                }
            }

            return new TabularColumnDefinition(column.Name, columnType, maxLength);
        }

        private static SqlBulkCopy CreateBulkCopy(SqlConnection connection, TableImportTarget target, IReadOnlyList<TabularColumnDefinition> columns, int batchSize)
        {
            var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = target.QualifiedNameWithDatabase,
                BulkCopyTimeout = 0,
                BatchSize = batchSize
            };

            foreach (var column in columns)
            {
                bulkCopy.ColumnMappings.Add(column.Name, column.Name);
            }

            return bulkCopy;
        }

        private static DataTable CreateBufferTable(IReadOnlyList<TabularColumnDefinition> columns)
        {
            var table = new DataTable();
            foreach (var column in columns)
            {
                var dataType = column.ColumnType switch
                {
                    TabularColumnType.Bit => typeof(bool),
                    TabularColumnType.Int => typeof(int),
                    TabularColumnType.BigInt => typeof(long),
                    TabularColumnType.Decimal => typeof(decimal),
                    TabularColumnType.Float => typeof(double),
                    TabularColumnType.DateTime2 => typeof(DateTime),
                    _ => typeof(string)
                };

                var columnDef = table.Columns.Add(column.Name, dataType);
                columnDef.AllowDBNull = true;
            }

            return table;
        }

        private static (string[] Headers, DataField[] Fields)? ReadNormalizedParquetHeader(ParquetReader reader)
        {
            var dataFields = reader.Schema.GetDataFields();
            if (dataFields == null || dataFields.Length == 0)
            {
                return null;
            }

            var headers = dataFields
                .Select(f => (f?.Name ?? string.Empty).Trim())
                .ToArray();

            return (headers, dataFields);
        }

        private static async Task<int> BulkCopyParquetRowsAsync(
            SqlBulkCopy bulkCopy,
            DataTable buffer,
            IReadOnlyList<TabularColumnDefinition> columns,
            ParquetReader reader,
            IReadOnlyList<DataField> fields,
            List<TabularImportError> errors,
            string fileName,
            int batchSize,
            CancellationToken cancellationToken)
        {
            var totalCopied = 0;
            var rowNumber = 0L;

            for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount; rowGroupIndex++)
            {
                using var rowGroupReader = reader.OpenRowGroupReader(rowGroupIndex);

                var parquetColumns = new Parquet.Data.DataColumn[fields.Count];
                for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    parquetColumns[fieldIndex] = await rowGroupReader.ReadColumnAsync(fields[fieldIndex]);
                }

                if (parquetColumns.Length == 0)
                {
                    continue;
                }

                var rowCount = parquetColumns[0].Data.Length;

                for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    var rowValues = new object[columns.Count];
                    var hasValue = false;
                    rowNumber++;

                    for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        var dataColumn = parquetColumns[columnIndex];
                        var value = dataColumn.Data.GetValue(rowIndex);
                        var converted = ConvertValue(value, columns[columnIndex], errors, fileName, rowNumber);

                        if (converted != DBNull.Value)
                        {
                            hasValue = true;
                        }

                        rowValues[columnIndex] = converted ?? DBNull.Value;
                    }

                    if (!hasValue)
                    {
                        continue;
                    }

                    buffer.Rows.Add(rowValues);

                    if (buffer.Rows.Count >= batchSize)
                    {
                        await bulkCopy.WriteToServerAsync(buffer, cancellationToken);
                        totalCopied += buffer.Rows.Count;
                        buffer.Clear();
                    }
                }
            }

            if (buffer.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(buffer, cancellationToken);
                totalCopied += buffer.Rows.Count;
                buffer.Clear();
            }

            return totalCopied;
        }

        private static void ValidateHeadersAgainstColumns(IReadOnlyList<string> headers, IReadOnlyList<TabularColumnDefinition> columns, string formatLabel)
        {
            if (headers.Count != columns.Count)
            {
                throw new InvalidOperationException($"The {formatLabel} files must share the same columns as the selected schema.");
            }

            for (var i = 0; i < headers.Count; i++)
            {
                if (!string.Equals(headers[i], columns[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"The {formatLabel} files must share the same columns as the selected schema.");
                }
            }
        }

        private static void EnsureUniqueHeaders(IEnumerable<string> headers)
        {
            var duplicates = headers
                .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException("Column headers must be unique.");
            }
        }

        private static object? ConvertValue(object? rawValue, TabularColumnDefinition column, List<TabularImportError> errors, string fileName, long rowNumber)
        {
            if (rawValue == null)
            {
                return DBNull.Value;
            }

            if (rawValue is DBNull)
            {
                return DBNull.Value;
            }

            if (rawValue is string stringValue)
            {
                stringValue = stringValue.Trim();
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return DBNull.Value;
                }

                return ConvertFromString(stringValue, column, errors, fileName, rowNumber);
            }

            if (column.ColumnType == TabularColumnType.DateTime2 && rawValue is DateTime dateTimeValue)
            {
                return dateTimeValue;
            }

            if ((column.ColumnType == TabularColumnType.Int || column.ColumnType == TabularColumnType.BigInt) && rawValue is long longValue)
            {
                return column.ColumnType == TabularColumnType.Int ? (object)(int)longValue : longValue;
            }

            if ((column.ColumnType == TabularColumnType.Int || column.ColumnType == TabularColumnType.BigInt) && rawValue is int intValue)
            {
                return column.ColumnType == TabularColumnType.Int ? intValue : (object)(long)intValue;
            }

            if (column.ColumnType == TabularColumnType.Decimal && rawValue is decimal decimalValue)
            {
                return decimalValue;
            }

            if (column.ColumnType == TabularColumnType.Float && rawValue is double doubleValue)
            {
                return doubleValue;
            }

            if (column.ColumnType == TabularColumnType.Float && rawValue is float floatValue)
            {
                return (double)floatValue;
            }

            if (column.ColumnType == TabularColumnType.Bit && rawValue is bool boolValue)
            {
                return boolValue;
            }

            if (column.ColumnType == TabularColumnType.NVarChar)
            {
                return rawValue.ToString()?.Trim();
            }

            return rawValue;
        }

        private static object ConvertFromString(string value, TabularColumnDefinition column, List<TabularImportError> errors, string fileName, long rowNumber)
        {
            switch (column.ColumnType)
            {
                case TabularColumnType.Bit:
                    if (bool.TryParse(value, out var boolValue))
                    {
                        return boolValue;
                    }

                    if (value == "0" || value == "1")
                    {
                        return value == "1";
                    }

                    errors.Add(new TabularImportError(fileName, rowNumber, column.Name, value, "Value must be a boolean."));
                    return DBNull.Value;
                case TabularColumnType.Int:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        return intValue;
                    }

                    errors.Add(new TabularImportError(fileName, rowNumber, column.Name, value, "Value must be an integer."));
                    return DBNull.Value;
                case TabularColumnType.BigInt:
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                    {
                        return longValue;
                    }

                    errors.Add(new TabularImportError(fileName, rowNumber, column.Name, value, "Value must be a 64-bit integer."));
                    return DBNull.Value;
                case TabularColumnType.Decimal:
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        return decimalValue;
                    }

                    errors.Add(new TabularImportError(fileName, rowNumber, column.Name, value, "Value must be a decimal number."));
                    return DBNull.Value;
                case TabularColumnType.Float:
                    if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                    {
                        return doubleValue;
                    }

                    errors.Add(new TabularImportError(fileName, rowNumber, column.Name, value, "Value must be a floating-point number."));
                    return DBNull.Value;
                case TabularColumnType.DateTime2:
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateValue))
                    {
                        return dateValue;
                    }

                    errors.Add(new TabularImportError(fileName, rowNumber, column.Name, value, "Value must be a valid date/time."));
                    return DBNull.Value;
                default:
                    return value;
            }
        }

        private sealed record ColumnMetadata(
            string Name,
            string TypeName,
            short MaxLength,
            byte Precision,
            byte Scale,
            bool IsNullable,
            bool IsIdentity,
            decimal? IdentitySeed,
            decimal? IdentityIncrement);

        private sealed record ReplaceStartResponse(Guid JobId, string StagingTable);

        private sealed record ReplaceChunkResponse(int RowsInserted, IReadOnlyList<TabularImportError> Errors);

        private sealed record ReplaceCommitResponse(string OldTableName, string Message);

        private sealed record ReplaceCompletionResponse(string Message);

        private sealed record DatasetResolution(Metadonnee Metadonnee, NotebookApi.NotebookApiBaseController.NotebookApiTableType TableType, TableImportTarget Target);

        private sealed record IndexMetadata(bool Enabled, string? TimeColumn, string? IdColumn, string? IncludeColumn);
    }
}
