using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Dataportal.Controllers
{
    [ApiController]
    [Route("api/exports")]
    public class ExportsController : ControllerBase
    {
        private const int DefaultMaxRowsPerPart = 1_000_000;
        private const int MaxAllowedRowsPerPart = 5_000_000;
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

            maxRowsPerPart = Math.Clamp(maxRowsPerPart, 1, MaxAllowedRowsPerPart);

            var metadonnee = await _context.Metadonnee
                .AsNoTracking()
                .Include(m => m.Utilisateur)
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m =>
                    (m.Donnees != null && m.Donnees.NomDeLaTable != null && m.Donnees.NomDeLaTable.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                    (m.DonneesEventLogs != null && m.DonneesEventLogs.NomDeLaTable != null && m.DonneesEventLogs.NomDeLaTable.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                    (m.DonneesContexteEnvironnemental != null && m.DonneesContexteEnvironnemental.NomDeLaTable != null && m.DonneesContexteEnvironnemental.NomDeLaTable.Equals(name, StringComparison.OrdinalIgnoreCase)),
                    cancellationToken);

            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!HttpContextUserHelper.CanCurrentUserAccessMetadonnee(HttpContext, _context, metadonnee, out var requiresAuthentication))
            {
                return requiresAuthentication ? Challenge() : Forbid();
            }

            if (!metadonnee.AutoriserLeTelechargement)
            {
                return Forbid();
            }

            var datasetInfo = ResolveDatasetInfo(metadonnee, name);
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

            return new FileCallbackResult("application/zip", async (outputStream, writeCancellationToken) =>
            {
                await StreamTableAsZipAsync(outputStream, sanitizedTable, datasetInfo, maxRowsPerPart, writeCancellationToken);
            })
            {
                FileDownloadName = downloadFileName
            };
        }

        private async Task StreamTableAsZipAsync(Stream outputStream, string sanitizedTableName, DatasetInfo datasetInfo, int maxRowsPerPart, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand($"SELECT * FROM {sanitizedTableName}", connection);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult, cancellationToken);

            var columnNames = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();

            await using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);

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
                writer = new StreamWriter(currentEntryStream, new UTF8Encoding(false), leaveOpen: true);
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
                writer.Dispose();
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

                var values = new string[columnNames.Length];
                for (var i = 0; i < columnNames.Length; i++)
                {
                    values[i] = EscapeCsv(FormatValue(reader.GetValue(i)));
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

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, ManifestSerializerOptions, cancellationToken);
            }
        }

        private static string? SanitizeTableName(string tableName)
        {
            var segments = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment.Length == 0)
                {
                    return null;
                }

                if (i > 0)
                {
                    builder.Append('.');
                }

                builder.Append('[');
                builder.Append(segment.Replace("]", "]]", StringComparison.Ordinal));
                builder.Append(']');
            }

            return builder.ToString();
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

            return value switch
            {
                DateTime dateTime => dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
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
        }

        private sealed class ExportManifestPart
        {
            public required string File { get; init; }
            public required int PartNumber { get; init; }
            public required long RowCount { get; init; }
            public long? FirstRowIndex { get; init; }
            public long? LastRowIndex { get; init; }
        }
    }
}
