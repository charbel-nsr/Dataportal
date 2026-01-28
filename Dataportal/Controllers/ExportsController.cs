using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Dataportal.Controllers
{
    [ApiController]
    [Route("api/exports")]
    public class ExportsController : ControllerBase
    {
        private const int DefaultMaxRowsPerPart = 1_000_000;
        private const int MaxAllowedRowsPerPart = 5_000_000;
        private const int MaxTrackedWarnings = 100;
        private static readonly char[] CsvSpecialChars = new[] { '"', ',', '\n', '\r' };
        private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ApplicationDbContext _context;

        public ExportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("table/{name}/zip")]
        public async Task<IActionResult> DownloadTableAsZip(string name, [FromQuery] int maxRowsPerPart = DefaultMaxRowsPerPart, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Table name is required.");
            }

            var normalizedName = name.Trim();
            if (normalizedName.Length == 0)
            {
                return BadRequest("Table name is required.");
            }

            maxRowsPerPart = Math.Clamp(maxRowsPerPart, 1, MaxAllowedRowsPerPart);

            var normalizedLookup = normalizedName.ToUpperInvariant();

            var metadonnee = await _context.Metadonnee
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

            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!HttpContextUserHelper.CanCurrentUserAccessMetadonnee(HttpContext, _context, metadonnee, out var requiresAuthentication))
            {
                return requiresAuthentication ? Challenge() : Forbid();
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

            if (!metadonnee.AutoriserLeTelechargement)
            {
                return Forbid();
            }

            var datasetInfo = ResolveDatasetInfo(metadonnee, normalizedName);
            if (datasetInfo == null)
            {
                return NotFound();
            }

            var sanitizedTable = SanitizeTableName(datasetInfo.TableName);
            if (sanitizedTable == null)
            {
                return BadRequest("Invalid table name.");
            }

            var downloadFileName = $"{SanitizeFileName(datasetInfo.DownloadFileName)}.zip";

            var bodyControlFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (bodyControlFeature != null && !bodyControlFeature.AllowSynchronousIO)
            {
                bodyControlFeature.AllowSynchronousIO = true;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, HttpContext.RequestAborted);
            var exportCancellationToken = linkedCts.Token;

            try
            {
                await StreamTableAsZipAsync(Response, sanitizedTable, datasetInfo, maxRowsPerPart, downloadFileName, exportCancellationToken);
                return new EmptyResult();
            }
            catch (SqlObjectNotFoundException sqlObjectNotFoundException) when (!Response.HasStarted)
            {
                var problem = new ProblemDetails
                {
                    Title = "Table not found",
                    Detail = sqlObjectNotFoundException.Message,
                    Status = StatusCodes.Status404NotFound
                };

                problem.Extensions["requestedName"] = datasetInfo.TableName;
                problem.Extensions["sanitizedSqlLiteral"] = sqlObjectNotFoundException.Sanitized.SqlLiteral;
                problem.Extensions["attemptedSqlLiterals"] = sqlObjectNotFoundException.AttemptedSqlLiterals;

                return NotFound(problem);
            }
            catch (SqlException sqlException) when (IsMissingObject(sqlException))
            {
                return NotFound();
            }
            catch (SqlException sqlException) when (IsPermissionDenied(sqlException))
            {
                return Forbid();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception) when (!Response.HasStarted)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpGet("table/{name}/diagnostics")]
        public async Task<IActionResult> GetTableDiagnostics(string name, [FromQuery] int maxRowsPerPart = DefaultMaxRowsPerPart, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Table name is required.");
            }

            var normalizedName = name.Trim();
            if (normalizedName.Length == 0)
            {
                return BadRequest("Table name is required.");
            }

            maxRowsPerPart = Math.Clamp(maxRowsPerPart, 1, MaxAllowedRowsPerPart);

            var normalizedLookup = normalizedName.ToUpperInvariant();

            var metadonnee = await _context.Metadonnee
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

            if (metadonnee == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = $"No dataset metadata is associated with table '{normalizedName}'."
                });
            }

            if (!HttpContextUserHelper.CanCurrentUserAccessMetadonnee(HttpContext, _context, metadonnee, out var requiresAuthentication))
            {
                return requiresAuthentication ? Challenge() : Forbid();
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

            if (!metadonnee.AutoriserLeTelechargement)
            {
                return Forbid();
            }

            var datasetInfo = ResolveDatasetInfo(metadonnee, normalizedName);
            if (datasetInfo == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset section not found",
                    Detail = $"No dataset section is configured for table '{normalizedName}'."
                });
            }

            var sanitizedTable = SanitizeTableName(datasetInfo.TableName);
            if (sanitizedTable == null)
            {
                return BadRequest("Invalid table name.");
            }

            var downloadFileName = $"{SanitizeFileName(datasetInfo.DownloadFileName)}.zip";

            await using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            var resolution = await ResolveSqlTargetAsync(connection, sanitizedTable, cancellationToken);

            var response = new ExportDiagnosticsResponse
            {
                Dataset = new ExportDiagnosticsDataset
                {
                    Id = datasetInfo.DatasetId,
                    Name = datasetInfo.DatasetName,
                    DisplayName = datasetInfo.DisplayName,
                    Section = datasetInfo.Section
                },
                RequestedTableName = datasetInfo.TableName,
                NormalizedRequest = normalizedName,
                SanitizedSqlLiteral = sanitizedTable.SqlLiteral,
                AttemptedSqlLiterals = resolution.AttemptedSqlLiterals,
                ResolvedSqlLiteral = resolution.ResolvedSqlLiteral,
                Resolved = resolution.Resolved,
                DownloadFileName = downloadFileName,
                MaxRowsPerPart = maxRowsPerPart
            };

            if (!resolution.Resolved)
            {
                response.Message = "No SQL object could be resolved for the requested table name.";
            }

            return Ok(response);
        }

        private async Task StreamTableAsZipAsync(HttpResponse response, SanitizedTableName sanitizedTableName, DatasetInfo datasetInfo, int maxRowsPerPart, string downloadFileName, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            var resolution = await ResolveSqlTargetAsync(connection, sanitizedTableName, cancellationToken);
            if (!resolution.Resolved)
            {
                throw new SqlObjectNotFoundException(datasetInfo.TableName, sanitizedTableName, resolution.AttemptedSqlLiterals);
            }

            await using var command = new SqlCommand($"SELECT * FROM {resolution.ResolvedSqlLiteral}", connection);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult, cancellationToken);

            var columnNames = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();
            var formattingWarnings = new List<ExportManifestWarning>(capacity: MaxTrackedWarnings);
            var warningsOverflow = false;

            var headers = response.GetTypedHeaders();
            headers.ContentType = new MediaTypeHeaderValue("application/zip");
            headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = downloadFileName,
                FileNameStar = downloadFileName
            };

            response.Headers["X-Export-Sanitized-Name"] = sanitizedTableName.SqlLiteral;
            if (resolution.ResolvedSqlLiteral != null)
            {
                response.Headers["X-Export-Resolved-Name"] = resolution.ResolvedSqlLiteral;
            }

            response.StatusCode = StatusCodes.Status200OK;
            await response.StartAsync(cancellationToken);

            var responseStream = response.BodyWriter.AsStream(leaveOpen: true);
            using (var archive = new ZipArchive(responseStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifest = new ExportManifest
                {
                    DatasetId = datasetInfo.DatasetId,
                    DatasetName = datasetInfo.DatasetName,
                    DisplayName = datasetInfo.DisplayName,
                    Section = datasetInfo.Section,
                    Table = datasetInfo.TableName,
                    MaxRowsPerPart = maxRowsPerPart,
                    GeneratedAtUtc = DateTime.UtcNow
                };

                int partNumber = 0;
                long totalRowCount = 0;
                long currentPartRowCount = 0;
                long currentPartFirstRowIndex = 1;
                string? currentEntryName = null;
                ZipArchiveEntry? currentEntry = null;
                Stream? currentEntryStream = null;
                StreamWriter? writer = null;

                async Task OpenPartAsync()
                {
                    partNumber++;
                    currentEntryName = $"data_part{partNumber:000}.csv";
                    currentEntry = archive.CreateEntry(currentEntryName, CompressionLevel.Optimal);
                    currentEntryStream = currentEntry.Open();
                    writer = new StreamWriter(currentEntryStream, new UTF8Encoding(false), bufferSize: 8192, leaveOpen: true);
                    var headerLine = string.Join(",", columnNames.Select(EscapeCsv));
                    await writer.WriteLineAsync(headerLine);
                    currentPartRowCount = 0;
                    currentPartFirstRowIndex = totalRowCount + 1;
                }

                async Task ClosePartAsync()
                {
                    if (writer == null || currentEntryStream == null || currentEntryName == null)
                    {
                        return;
                    }

                    await writer.FlushAsync(cancellationToken);
                    await currentEntryStream.FlushAsync(cancellationToken);
                    await writer.DisposeAsync();
                    currentEntryStream.Dispose();
                    writer = null;
                    currentEntryStream = null;

                    manifest.Parts.Add(new ExportManifestPart
                    {
                        File = currentEntryName,
                        PartNumber = partNumber,
                        RowCount = currentPartRowCount,
                        FirstRowIndex = currentPartRowCount > 0 ? currentPartFirstRowIndex : (long?)null,
                        LastRowIndex = currentPartRowCount > 0 ? currentPartFirstRowIndex + currentPartRowCount - 1 : (long?)null
                    });

                    currentEntryName = null;
                }

                while (await reader.ReadAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (writer == null)
                    {
                        await OpenPartAsync();
                    }

                    var rowIndex = totalRowCount + 1;
                    var values = new string[columnNames.Length];
                    for (var i = 0; i < columnNames.Length; i++)
                    {
                        object? rawValue = null;
                        try
                        {
                            rawValue = reader.GetValue(i);
                            values[i] = EscapeCsv(FormatValue(rawValue));
                        }
                        catch (Exception ex)
                        {
                            values[i] = EscapeCsv($"[format-error:{ex.GetType().Name}]");

                            if (formattingWarnings.Count < MaxTrackedWarnings)
                            {
                                var fieldType = SafeGetFieldTypeName(reader, i);
                                formattingWarnings.Add(new ExportManifestWarning
                                {
                                    Column = columnNames[i],
                                    RowIndex = rowIndex,
                                    Error = ex.GetType().Name,
                                    Message = Truncate(ex.Message),
                                    Type = rawValue?.GetType().FullName ?? fieldType ?? "unknown"
                                });
                            }
                            else
                            {
                                warningsOverflow = true;
                            }
                        }
                    }

                    await writer!.WriteLineAsync(string.Join(",", values));
                    currentPartRowCount++;
                    totalRowCount++;

                    if (currentPartRowCount % 100 == 0)
                    {
                        await writer.FlushAsync(cancellationToken);
                    }

                    if (currentPartRowCount >= maxRowsPerPart)
                    {
                        await ClosePartAsync();
                    }
                }

                if (writer == null)
                {
                    if (totalRowCount == 0)
                    {
                        await OpenPartAsync();
                        await ClosePartAsync();
                    }
                }
                else
                {
                    await ClosePartAsync();
                }

                manifest.TotalRowCount = totalRowCount;

                if (formattingWarnings.Count > 0)
                {
                    manifest.Warnings.AddRange(formattingWarnings);
                }

                if (warningsOverflow)
                {
                    manifest.WarningsTruncated = true;
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, ManifestSerializerOptions, cancellationToken);
                }
            }

            await response.Body.FlushAsync(cancellationToken);
        }

        private static bool IsMissingObject(SqlException exception) => exception.Number is 208 or 4121;

        private static bool IsPermissionDenied(SqlException exception) => exception.Number is 229 or 262;

        private static SanitizedTableName? SanitizeTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return null;
            }

            var trimmed = tableName.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            var segments = new List<string>();
            var buffer = new StringBuilder();
            var state = IdentifierParseState.Default;
            var encounteredDotSeparator = false;

            bool CommitSegment()
            {
                var segment = buffer.ToString().Trim();
                buffer.Clear();

                if (segment.Length == 0)
                {
                    return false;
                }

                if (ContainsInvalidIdentifierCharacters(segment))
                {
                    return false;
                }

                segments.Add(segment);
                return true;
            }

            foreach (var ch in trimmed)
            {
                switch (state)
                {
                    case IdentifierParseState.Default:
                        if (ch == '.')
                        {
                            if (!CommitSegment())
                            {
                                return null;
                            }

                            encounteredDotSeparator = true;
                            continue;
                        }

                        if (char.IsWhiteSpace(ch))
                        {
                            if (buffer.Length == 0)
                            {
                                continue;
                            }

                            buffer.Append(ch);
                            continue;
                        }

                        if (ch == '[')
                        {
                            if (buffer.Length > 0)
                            {
                                return null;
                            }

                            state = IdentifierParseState.Bracketed;
                            continue;
                        }

                        if (ch == '"')
                        {
                            if (buffer.Length > 0)
                            {
                                return null;
                            }

                            state = IdentifierParseState.DoubleQuoted;
                            continue;
                        }

                        if (IsInvalidUnquotedCharacter(ch))
                        {
                            return null;
                        }

                        buffer.Append(ch);
                        break;

                    case IdentifierParseState.Bracketed:
                        if (ch == ']')
                        {
                            state = IdentifierParseState.Default;
                            continue;
                        }

                        if (IsInvalidSegmentCharacter(ch))
                        {
                            return null;
                        }

                        buffer.Append(ch);
                        break;

                    case IdentifierParseState.DoubleQuoted:
                        if (ch == '"')
                        {
                            state = IdentifierParseState.Default;
                            continue;
                        }

                        if (IsInvalidSegmentCharacter(ch))
                        {
                            return null;
                        }

                        buffer.Append(ch);
                        break;
                }
            }

            if (state != IdentifierParseState.Default)
            {
                return null;
            }

            if (buffer.Length > 0)
            {
                if (!CommitSegment())
                {
                    return null;
                }
            }

            if (segments.Count == 0)
            {
                return null;
            }

            var sqlLiteral = BuildBracketedIdentifier(segments);

            return new SanitizedTableName(trimmed, segments.AsReadOnly(), sqlLiteral, encounteredDotSeparator);
        }

        private static bool ContainsInvalidIdentifierCharacters(string segment)
        {
            foreach (var ch in segment)
            {
                if (char.IsControl(ch))
                {
                    return true;
                }

                if (ch == '[' || ch == ']')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInvalidUnquotedCharacter(char ch) => char.IsControl(ch) || ch == '[' || ch == ']' || ch == '"';

        private static bool IsInvalidSegmentCharacter(char ch) => char.IsControl(ch);

        private enum IdentifierParseState
        {
            Default,
            Bracketed,
            DoubleQuoted
        }

        private static async Task<SqlTargetResolutionResult> ResolveSqlTargetAsync(SqlConnection connection, SanitizedTableName sanitizedTableName, CancellationToken cancellationToken)
        {
            var candidates = BuildCandidateForms(sanitizedTableName);
            var attempts = candidates.AsReadOnly();

            foreach (var candidate in candidates)
            {
                await using var command = new SqlCommand($"SELECT TOP (0) 1 FROM {candidate}", connection);
                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    return new SqlTargetResolutionResult
                    {
                        AttemptedSqlLiterals = attempts,
                        ResolvedSqlLiteral = candidate
                    };
                }
                catch (SqlException sqlException) when (IsMissingObject(sqlException))
                {
                    // Try the next candidate.
                }
            }

            return new SqlTargetResolutionResult
            {
                AttemptedSqlLiterals = attempts
            };
        }

        private static List<string> BuildCandidateForms(SanitizedTableName sanitizedTableName)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();

            void AddCandidate(string candidate)
            {
                if (unique.Add(candidate))
                {
                    ordered.Add(candidate);
                }
            }

            AddCandidate(sanitizedTableName.SqlLiteral);

            if (sanitizedTableName.Segments.Count >= 3)
            {
                var schema = sanitizedTableName.Segments[^2];
                var table = sanitizedTableName.Segments[^1];
                var schemaQualified = $"[{schema.Replace("]", "]]", StringComparison.Ordinal)}].[{table.Replace("]", "]]", StringComparison.Ordinal)}]";
                AddCandidate(schemaQualified);
            }

            if (sanitizedTableName.Segments.Count == 1)
            {
                var dboQualified = $"[dbo].{sanitizedTableName.SqlLiteral}";
                AddCandidate(dboQualified);
            }

            if (sanitizedTableName.EncounteredDotSeparator)
            {
                var fullLiteral = $"[{sanitizedTableName.OriginalIdentifier.Replace("]", "]]", StringComparison.Ordinal)}]";
                AddCandidate(fullLiteral);
            }

            return ordered;
        }

        private static string BuildBracketedIdentifier(IReadOnlyList<string> segments)
        {
            var result = new StringBuilder();
            for (var i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                {
                    result.Append('.');
                }

                result.Append('[');
                result.Append(segments[i].Replace("]", "]]", StringComparison.Ordinal));
                result.Append(']');
            }

            return result.ToString();
        }

        private sealed class SanitizedTableName
        {
            public SanitizedTableName(string originalIdentifier, IReadOnlyList<string> segments, string sqlLiteral, bool encounteredDotSeparator)
            {
                OriginalIdentifier = originalIdentifier;
                Segments = segments;
                SqlLiteral = sqlLiteral;
                EncounteredDotSeparator = encounteredDotSeparator;
            }

            public string OriginalIdentifier { get; }

            public IReadOnlyList<string> Segments { get; }

            public string SqlLiteral { get; }

            public bool EncounteredDotSeparator { get; }
        }

        private sealed class SqlTargetResolutionResult
        {
            public required IReadOnlyList<string> AttemptedSqlLiterals { get; init; }

            public string? ResolvedSqlLiteral { get; init; }

            public bool Resolved => !string.IsNullOrEmpty(ResolvedSqlLiteral);
        }

        private sealed class SqlObjectNotFoundException : Exception
        {
            public SqlObjectNotFoundException(string requestedName, SanitizedTableName sanitized, IReadOnlyList<string> attemptedSqlLiterals)
                : base($"No SQL object was found for requested table '{requestedName}'. Attempts: {string.Join(", ", attemptedSqlLiterals)}.")
            {
                Sanitized = sanitized;
                AttemptedSqlLiterals = attemptedSqlLiterals;
            }

            public SanitizedTableName Sanitized { get; }

            public IReadOnlyList<string> AttemptedSqlLiterals { get; }
        }

        private sealed class ExportDiagnosticsResponse
        {
            public required ExportDiagnosticsDataset Dataset { get; init; }

            public required string RequestedTableName { get; init; }

            public required string NormalizedRequest { get; init; }

            public required string SanitizedSqlLiteral { get; init; }

            public required IReadOnlyList<string> AttemptedSqlLiterals { get; init; }

            public string? ResolvedSqlLiteral { get; init; }

            public bool Resolved { get; init; }

            public required string DownloadFileName { get; init; }

            public required int MaxRowsPerPart { get; init; }

            public string? Message { get; set; }
        }

        private sealed class ExportDiagnosticsDataset
        {
            public required int Id { get; init; }

            public required string Name { get; init; }

            public required string DisplayName { get; init; }

            public required string Section { get; init; }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(fileName.Length);

            foreach (var ch in fileName)
            {
                builder.Append(invalidChars.Contains(ch) ? '_' : ch);
            }

            var sanitized = builder.ToString().Trim();
            if (string.IsNullOrEmpty(sanitized))
            {
                return "dataset-export";
            }

            return sanitized.Length > 120 ? sanitized[..120] : sanitized;
        }

        private static string FormatValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            if (value is INullable nullable && nullable.IsNull)
            {
                return string.Empty;
            }

            return value switch
            {
                string str => str,
                char ch => ch.ToString(),
                char[] charArray => new string(charArray),
                ReadOnlyMemory<char> charMemory => new string(charMemory.Span),
                Memory<char> mutableCharMemory => new string(mutableCharMemory.Span),
                byte[] bytes => Convert.ToBase64String(bytes),
                Memory<byte> mutableByteMemory => Convert.ToBase64String(mutableByteMemory.Span),
                ReadOnlyMemory<byte> byteMemory => Convert.ToBase64String(byteMemory.Span),
                ArraySegment<byte> byteSegment => Convert.ToBase64String(byteSegment.AsSpan()),
                SqlBytes sqlBytes => Convert.ToBase64String(sqlBytes.Value),
                SqlBinary sqlBinary => Convert.ToBase64String(sqlBinary.Value),
                SqlChars sqlChars => new string(sqlChars.Value),
                SqlString sqlString => sqlString.Value,
                SqlXml sqlXml => sqlXml.Value,
                SqlGuid sqlGuid => sqlGuid.Value.ToString(),
                SqlBoolean sqlBoolean => sqlBoolean.Value ? "true" : "false",
                SqlByte sqlByte => sqlByte.Value.ToString(CultureInfo.InvariantCulture),
                SqlInt16 sqlInt16 => sqlInt16.Value.ToString(CultureInfo.InvariantCulture),
                SqlInt32 sqlInt32 => sqlInt32.Value.ToString(CultureInfo.InvariantCulture),
                SqlInt64 sqlInt64 => sqlInt64.Value.ToString(CultureInfo.InvariantCulture),
                SqlDecimal sqlDecimal => sqlDecimal.Value.ToString(CultureInfo.InvariantCulture),
                SqlDouble sqlDouble => sqlDouble.Value.ToString(CultureInfo.InvariantCulture),
                SqlSingle sqlSingle => sqlSingle.Value.ToString(CultureInfo.InvariantCulture),
                SqlMoney sqlMoney => sqlMoney.Value.ToString(CultureInfo.InvariantCulture),
                DateTime dateTime => dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly timeOnly => timeOnly.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
                bool boolValue => boolValue ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => FormatComplexValue(value)
            };
        }

        private static string FormatComplexValue(object value)
        {
            var type = value.GetType();

            if (type.FullName?.StartsWith("Microsoft.SqlServer.Types.", StringComparison.Ordinal) == true)
            {
                var isNullProperty = type.GetProperty("IsNull", BindingFlags.Instance | BindingFlags.Public);
                if (isNullProperty?.PropertyType == typeof(bool))
                {
                    var isNull = (bool)isNullProperty.GetValue(value)!;
                    if (isNull)
                    {
                        return string.Empty;
                    }
                }

                var toStringMethod = type.GetMethod("ToString", Type.EmptyTypes);
                if (toStringMethod != null)
                {
                    var result = toStringMethod.Invoke(value, null);
                    if (result != null)
                    {
                        return Convert.ToString(result, CultureInfo.InvariantCulture) ?? result.ToString() ?? string.Empty;
                    }
                }
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty;
        }

        private static string? Truncate(string? value, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static string? SafeGetFieldTypeName(SqlDataReader reader, int ordinal)
        {
            try
            {
                return reader.GetFieldType(ordinal)?.FullName;
            }
            catch
            {
                return null;
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.IndexOfAny(CsvSpecialChars) >= 0
                ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
                : value;
        }

        private static DatasetInfo? ResolveDatasetInfo(Metadonnee metadonnee, string requestedName)
        {
            if (metadonnee.Donnees?.NomDeLaTable != null && metadonnee.Donnees.NomDeLaTable.Equals(requestedName, StringComparison.OrdinalIgnoreCase))
            {
                var label = string.IsNullOrWhiteSpace(metadonnee.Donnees.Libelle) ? "data" : metadonnee.Donnees.Libelle;
                return new DatasetInfo
                {
                    DatasetId = metadonnee.Id,
                    DatasetName = metadonnee.Nom,
                    DisplayName = label,
                    Section = "data",
                    TableName = metadonnee.Donnees.NomDeLaTable,
                    DownloadFileName = BuildDownloadName(metadonnee.Nom, label, "data")
                };
            }

            if (metadonnee.DonneesEventLogs?.NomDeLaTable != null && metadonnee.DonneesEventLogs.NomDeLaTable.Equals(requestedName, StringComparison.OrdinalIgnoreCase))
            {
                var label = string.IsNullOrWhiteSpace(metadonnee.DonneesEventLogs.Libelle) ? "event_logs" : metadonnee.DonneesEventLogs.Libelle;
                return new DatasetInfo
                {
                    DatasetId = metadonnee.Id,
                    DatasetName = metadonnee.Nom,
                    DisplayName = label,
                    Section = "eventLogs",
                    TableName = metadonnee.DonneesEventLogs.NomDeLaTable,
                    DownloadFileName = BuildDownloadName(metadonnee.Nom, label, "eventLogs")
                };
            }

            if (metadonnee.DonneesContexteEnvironnemental?.NomDeLaTable != null && metadonnee.DonneesContexteEnvironnemental.NomDeLaTable.Equals(requestedName, StringComparison.OrdinalIgnoreCase))
            {
                var label = string.IsNullOrWhiteSpace(metadonnee.DonneesContexteEnvironnemental.Libelle) ? "environmental_context" : metadonnee.DonneesContexteEnvironnemental.Libelle;
                return new DatasetInfo
                {
                    DatasetId = metadonnee.Id,
                    DatasetName = metadonnee.Nom,
                    DisplayName = label,
                    Section = "environmentalContext",
                    TableName = metadonnee.DonneesContexteEnvironnemental.NomDeLaTable,
                    DownloadFileName = BuildDownloadName(metadonnee.Nom, label, "environment")
                };
            }

            return null;
        }

        private static string BuildDownloadName(params string?[] parts)
        {
            var result = new List<string>();
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var trimmed = part.Trim();
                if (result.Any(existing => existing.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(trimmed);
            }

            return result.Count > 0 ? string.Join("_", result) : "dataset";
        }

        private sealed class DatasetInfo
        {
            public required int DatasetId { get; init; }
            public required string DatasetName { get; init; }
            public required string DisplayName { get; init; }
            public required string Section { get; init; }
            public required string TableName { get; init; }
            public required string DownloadFileName { get; init; }
        }

        private sealed class ExportManifest
        {
            public required int DatasetId { get; init; }
            public required string DatasetName { get; init; }
            public required string DisplayName { get; init; }
            public required string Section { get; init; }
            public required string Table { get; init; }
            public required int MaxRowsPerPart { get; init; }
            public required DateTime GeneratedAtUtc { get; init; }
            public long TotalRowCount { get; set; }
            public List<ExportManifestPart> Parts { get; } = new();
            public List<ExportManifestWarning> Warnings { get; } = new();
            public bool WarningsTruncated { get; set; }
        }

        private sealed class ExportManifestPart
        {
            public required string File { get; init; }
            public required int PartNumber { get; init; }
            public required long RowCount { get; init; }
            public long? FirstRowIndex { get; init; }
            public long? LastRowIndex { get; init; }
        }

        private sealed class ExportManifestWarning
        {
            public required string Column { get; init; }
            public required long RowIndex { get; init; }
            public required string Error { get; init; }
            public string? Message { get; init; }
            public string? Type { get; init; }
        }
    }
}
