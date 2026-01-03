using Dataportal.Context;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dataportal.Services
{
    public class TabularFileImportService : ITabularFileImporter
    {
        private readonly string _connectionString;
        private static readonly object EncodingLock = new();
        private static bool _encodingRegistered;
        private const int DefaultSampleRows = 5000;
        private const int MaxLoggedErrors = 200;
        private const int DefaultNVarCharLength = 255;
        private const int MaxInlineNVarCharLength = 4000;

        private enum TabularFormat
        {
            Csv,
            Excel,
            Parquet,
            ZippedCsv
        }

        private sealed class ColumnInferenceState
        {
            public ColumnInferenceState(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public bool HasValue { get; set; }

            public bool FitsBoolean { get; set; } = true;

            public bool FitsInt32 { get; set; } = true;

            public bool FitsInt64 { get; set; } = true;

            public bool FitsDecimal { get; set; } = true;

            public bool FitsFloat { get; set; } = true;

            public bool FitsDateTime { get; set; } = true;

            public int MaxLength { get; set; }
        }

        public TabularFileImportService(ApplicationDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The database connection string could not be found.");

            EnsureEncodingProviderRegistered();
        }

        public async Task<IReadOnlyList<TabularColumnDefinition>> InferColumnsAsync(IEnumerable<IFormFile> files, int sampleSize = DefaultSampleRows)
        {
            ArgumentNullException.ThrowIfNull(files);

            var (fileList, format, formatLabel) = NormalizeFiles(files);
            return format switch
            {
                TabularFormat.Csv => await InferCsvAsync(fileList, formatLabel, sampleSize),
                TabularFormat.Excel => await InferExcelAsync(fileList, formatLabel, sampleSize),
                TabularFormat.Parquet => await InferParquetAsync(fileList, formatLabel, sampleSize),
                TabularFormat.ZippedCsv => await InferZippedCsvAsync(fileList, formatLabel, sampleSize),
                _ => throw new InvalidOperationException("Unsupported format.")
            };
        }

        public async Task<TabularImportResult> ImportAsync(TableImportTarget target, IEnumerable<IFormFile> files, IReadOnlyList<TabularColumnDefinition> columns, int batchSize = 5000)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(columns);

            var (fileList, format, formatLabel) = NormalizeFiles(files);
            if (columns.Count == 0)
            {
                throw new InvalidOperationException("No column definitions were provided.");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            return format switch
            {
                TabularFormat.Csv => await ImportCsvAsync(connection, target, fileList, formatLabel, columns, batchSize),
                TabularFormat.Excel => await ImportExcelAsync(connection, target, fileList, formatLabel, columns, batchSize),
                TabularFormat.Parquet => await ImportParquetAsync(connection, target, fileList, formatLabel, columns, batchSize),
                TabularFormat.ZippedCsv => await ImportZippedCsvAsync(connection, target, fileList, formatLabel, columns, batchSize),
                _ => throw new InvalidOperationException("Only CSV, XLSX, Parquet, or CSV.zip files are supported at this time.")
            };
        }

        public async Task ImportAsync(TableImportTarget target, IEnumerable<IFormFile> files)
        {
            var inferredColumns = await InferColumnsAsync(files);
            await ImportAsync(target, files, inferredColumns);
        }

        public async Task DropTableAsync(TableImportTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await DropSqlTableIfExists(connection, target);
        }

        private static void EnsureEncodingProviderRegistered()
        {
            if (_encodingRegistered)
            {
                return;
            }

            lock (EncodingLock)
            {
                if (_encodingRegistered)
                {
                    return;
                }

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encodingRegistered = true;
            }
        }

        private static string? NormalizeExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            if (fileName.EndsWith(".csv.zip", StringComparison.OrdinalIgnoreCase))
            {
                return ".csv.zip";
            }

            var extension = Path.GetExtension(fileName);
            return string.IsNullOrWhiteSpace(extension)
                ? null
                : extension.ToLowerInvariant();
        }

        private (List<IFormFile> Files, TabularFormat Format, string FormatLabel) NormalizeFiles(IEnumerable<IFormFile> files)
        {
            var fileList = files
                .Where(f => f != null)
                .ToList();

            if (fileList.Count == 0)
            {
                throw new InvalidOperationException("Les fichiers fournis sont vides.");
            }

            var extension = NormalizeExtension(fileList[0].FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new InvalidOperationException("Unable to determine the type of the imported files.");
            }

            if (fileList.Any(f => !string.Equals(NormalizeExtension(f.FileName), extension, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("All files imported for this step must be of the same type (CSV, XLSX, Parquet, or CSV.zip).");
            }

            var format = MapExtensionToFormat(extension);
            var label = GetFormatLabel(format);

            return (fileList, format, label);
        }

        private static string GetFormatLabel(TabularFormat format)
        {
            return format switch
            {
                TabularFormat.Csv => "CSV",
                TabularFormat.Excel => "XLSX",
                TabularFormat.Parquet => "Parquet",
                TabularFormat.ZippedCsv => "CSV.zip",
                _ => "imported"
            };
        }

        private static TabularFormat MapExtensionToFormat(string extension)
        {
            return extension switch
            {
                ".csv" => TabularFormat.Csv,
                ".xlsx" => TabularFormat.Excel,
                ".parquet" => TabularFormat.Parquet,
                ".csv.zip" => TabularFormat.ZippedCsv,
                _ => throw new InvalidOperationException("Only CSV, XLSX, Parquet, or CSV.zip files are supported at this time.")
            };
        }

        private static ColumnInferenceState[] InitializeStates(IReadOnlyList<string> headers)
        {
            return headers.Select(h => new ColumnInferenceState(h)).ToArray();
        }

        private static bool TryParseBooleanCandidate(string value, out bool parsed)
        {
            if (bool.TryParse(value, out parsed))
            {
                return true;
            }

            if (value == "1")
            {
                parsed = true;
                return true;
            }

            if (value == "0")
            {
                parsed = false;
                return true;
            }

            if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                parsed = true;
                return true;
            }

            if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                parsed = false;
                return true;
            }

            parsed = false;
            return false;
        }

        private static bool TryParseDateTimeValue(string raw, out DateTime parsed)
        {
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return true;
            }

            if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return true;
            }

            return false;
        }

        private static void UpdateInferenceState(ColumnInferenceState state, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            state.HasValue = true;
            var trimmed = raw.Trim();
            state.MaxLength = Math.Max(state.MaxLength, trimmed.Length);

            if (!TryParseBooleanCandidate(trimmed, out _))
            {
                state.FitsBoolean = false;
            }

            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                state.FitsInt32 = false;
            }

            if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                state.FitsInt64 = false;
            }

            if (!decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                && !decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.CurrentCulture, out _))
            {
                state.FitsDecimal = false;
            }

            if (!double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                && !double.TryParse(trimmed, NumberStyles.Any, CultureInfo.CurrentCulture, out _))
            {
                state.FitsFloat = false;
            }

            if (!TryParseDateTimeValue(trimmed, out _))
            {
                state.FitsDateTime = false;
            }
        }

        private static TabularColumnDefinition ToDefinition(ColumnInferenceState state)
        {
            var columnType = ResolveType(state);
            var length = columnType == TabularColumnType.NVarChar
                ? (int?)Math.Max(state.MaxLength, DefaultNVarCharLength)
                : null;

            return new TabularColumnDefinition(state.Name, columnType, length);
        }

        private static TabularColumnType ResolveType(ColumnInferenceState state)
        {
            if (!state.HasValue)
            {
                return TabularColumnType.NVarChar;
            }

            if (state.FitsBoolean)
            {
                return TabularColumnType.Bit;
            }

            if (state.FitsDateTime)
            {
                return TabularColumnType.DateTime2;
            }

            if (state.FitsInt32)
            {
                return TabularColumnType.Int;
            }

            if (state.FitsInt64)
            {
                return TabularColumnType.BigInt;
            }

            if (state.FitsDecimal)
            {
                return TabularColumnType.Decimal;
            }

            if (state.FitsFloat)
            {
                return TabularColumnType.Float;
            }

            return TabularColumnType.NVarChar;
        }

        private async Task<IReadOnlyList<TabularColumnDefinition>> InferCsvAsync(IReadOnlyList<IFormFile> files, string formatLabel, int sampleSize)
        {
            List<string>? headers = null;
            ColumnInferenceState[]? states = null;
            var rowsSeen = 0;

            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);
                using var parser = CreateCsvParser(reader);

                var normalizedHeader = ReadNormalizedCsvHeader(parser);
                if (normalizedHeader == null || normalizedHeader.Length == 0)
                {
                    continue;
                }

                if (headers == null)
                {
                    EnsureUniqueHeaders(normalizedHeader);
                    headers = normalizedHeader.ToList();
                    states = InitializeStates(headers);
                }
                else if (!HeadersMatch(headers, normalizedHeader))
                {
                    throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                }

                if (states == null)
                {
                    continue;
                }

                while (!parser.EndOfData && rowsSeen < sampleSize)
                {
                    var fields = parser.ReadFields();
                    if (fields == null || fields.Length == 0)
                    {
                        continue;
                    }

                    if (fields.Length != states.Length)
                    {
                        throw new InvalidOperationException("A row in the CSV file does not match the number of header columns.");
                    }

                    for (var i = 0; i < fields.Length; i++)
                    {
                        UpdateInferenceState(states[i], fields[i] ?? string.Empty);
                    }

                    rowsSeen++;
                }

                if (rowsSeen >= sampleSize)
                {
                    break;
                }
            }

            if (states == null)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return states.Select(ToDefinition).ToList();
        }

        private async Task<IReadOnlyList<TabularColumnDefinition>> InferZippedCsvAsync(IReadOnlyList<IFormFile> files, string formatLabel, int sampleSize)
        {
            List<string>? headers = null;
            ColumnInferenceState[]? states = null;
            var rowsSeen = 0;

            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

                var csvEntries = archive.Entries
                    .Where(entry => !string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (csvEntries.Count == 0)
                {
                    throw new InvalidOperationException("Chaque fichier CSV.zip doit contenir au moins un fichier CSV.");
                }

                foreach (var entry in csvEntries)
                {
                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream);
                    using var parser = CreateCsvParser(reader);

                    var normalizedHeader = ReadNormalizedCsvHeader(parser);
                    if (normalizedHeader == null || normalizedHeader.Length == 0)
                    {
                        continue;
                    }

                    if (headers == null)
                    {
                        EnsureUniqueHeaders(normalizedHeader);
                        headers = normalizedHeader.ToList();
                        states = InitializeStates(headers);
                    }
                    else if (!HeadersMatch(headers, normalizedHeader))
                    {
                        throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                    }

                    if (states == null)
                    {
                        continue;
                    }

                    while (!parser.EndOfData && rowsSeen < sampleSize)
                    {
                        var fields = parser.ReadFields();
                        if (fields == null || fields.Length == 0)
                        {
                            continue;
                        }

                        if (fields.Length != states.Length)
                        {
                            throw new InvalidOperationException("A row in the CSV file does not match the number of header columns.");
                        }

                        for (var i = 0; i < fields.Length; i++)
                        {
                            UpdateInferenceState(states[i], fields[i] ?? string.Empty);
                        }

                        rowsSeen++;
                    }

                    if (rowsSeen >= sampleSize)
                    {
                        break;
                    }
                }

                if (rowsSeen >= sampleSize)
                {
                    break;
                }
            }

            if (states == null)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return states.Select(ToDefinition).ToList();
        }

        private async Task<IReadOnlyList<TabularColumnDefinition>> InferExcelAsync(IReadOnlyList<IFormFile> files, string formatLabel, int sampleSize)
        {
            List<string>? headers = null;
            ColumnInferenceState[]? states = null;
            var rowsSeen = 0;

            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);

                do
                {
                    var normalizedHeader = ReadNormalizedExcelHeader(reader);
                    if (normalizedHeader == null || normalizedHeader.Length == 0)
                    {
                        continue;
                    }

                    if (headers == null)
                    {
                        EnsureUniqueHeaders(normalizedHeader);
                        headers = normalizedHeader.ToList();
                        states = InitializeStates(headers);
                    }
                    else if (!HeadersMatch(headers, normalizedHeader))
                    {
                        throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                    }

                    if (states == null)
                    {
                        continue;
                    }

                    while (reader.Read() && rowsSeen < sampleSize)
                    {
                        if (reader.FieldCount > states.Length)
                        {
                            ValidateExtraExcelColumns(reader, states.Length);
                        }

                        for (var i = 0; i < states.Length; i++)
                        {
                            object? cell = i < reader.FieldCount ? reader.GetValue(i) : null;
                            var cellValue = cell == null ? string.Empty : Convert.ToString(cell, CultureInfo.InvariantCulture) ?? string.Empty;
                            UpdateInferenceState(states[i], cellValue);
                        }

                        rowsSeen++;
                    }
                }
                while (reader.NextResult());

                if (rowsSeen >= sampleSize)
                {
                    break;
                }
            }

            if (states == null)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return states.Select(ToDefinition).ToList();
        }

        private async Task<IReadOnlyList<TabularColumnDefinition>> InferParquetAsync(IReadOnlyList<IFormFile> files, string formatLabel, int sampleSize)
        {
            List<string>? headers = null;
            ColumnInferenceState[]? states = null;
            var rowsSeen = 0;

            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                using var reader = await ParquetReader.CreateAsync(stream);

                var headerInfo = ReadNormalizedParquetHeader(reader);
                if (headerInfo == null || headerInfo.Value.Headers.Length == 0)
                {
                    continue;
                }

                var normalizedHeader = headerInfo.Value.Headers;

                if (headers == null)
                {
                    EnsureUniqueHeaders(normalizedHeader);
                    headers = normalizedHeader.ToList();
                    states = InitializeStates(headers);
                }
                else if (!HeadersMatch(headers, normalizedHeader))
                {
                    throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                }

                if (states == null)
                {
                    continue;
                }

                for (var rowGroupIndex = 0; rowGroupIndex < reader.RowGroupCount && rowsSeen < sampleSize; rowGroupIndex++)
                {
                    using var rowGroupReader = reader.OpenRowGroupReader(rowGroupIndex);
                    var fields = headerInfo.Value.Fields;

                    var columns = new Parquet.Data.DataColumn[fields.Length];
                    for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                    {
                        columns[fieldIndex] = await rowGroupReader.ReadColumnAsync(fields[fieldIndex]);
                    }

                    if (columns.Length == 0)
                    {
                        continue;
                    }

                    var rowCount = columns[0].Data.Length;
                    for (var rowIndex = 0; rowIndex < rowCount && rowsSeen < sampleSize; rowIndex++)
                    {
                        for (var columnIndex = 0; columnIndex < states.Length; columnIndex++)
                        {
                            var value = columns[columnIndex].Data.GetValue(rowIndex);
                            var stringValue = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                            UpdateInferenceState(states[columnIndex], stringValue);
                        }

                        rowsSeen++;
                    }
                }

                if (rowsSeen >= sampleSize)
                {
                    break;
                }
            }

            if (states == null)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return states.Select(ToDefinition).ToList();
        }

        private async Task<TabularImportResult> ImportCsvAsync(SqlConnection connection, TableImportTarget target, IReadOnlyList<IFormFile> files, string formatLabel, IReadOnlyList<TabularColumnDefinition> columns, int batchSize)
        {
            List<string>? headers = null;
            SqlBulkCopy? bulkCopy = null;
            DataTable? buffer = null;
            var result = new TabularImportResult();

            try
            {
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();
                    using var reader = new StreamReader(stream);
                    using var parser = CreateCsvParser(reader);

                    var normalizedHeader = ReadNormalizedCsvHeader(parser);
                    if (normalizedHeader == null || normalizedHeader.Length == 0)
                    {
                        continue;
                    }

                    if (headers == null)
                    {
                        EnsureUniqueHeaders(normalizedHeader);
                        ValidateHeadersAgainstColumns(normalizedHeader, columns, formatLabel);
                        headers = normalizedHeader.ToList();
                        await CreateSqlTableAsync(connection, target, columns);
                        bulkCopy = CreateBulkCopy(connection, target, columns, batchSize);
                        buffer = CreateBufferTable(columns);
                        result.TableCreated = true;
                    }
                    else if (!HeadersMatch(headers, normalizedHeader))
                    {
                        throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                    }

                    if (bulkCopy != null && buffer != null)
                    {
                        result.RowsCopied += await BulkCopyCsvRowsAsync(bulkCopy, buffer, columns, parser, result.Errors, file.FileName, batchSize);
                    }
                }
            }
            catch
            {
                if (result.TableCreated)
                {
                    await DropSqlTableIfExists(connection, target);
                }

                throw;
            }
            finally
            {
                DisposeBulkCopy(bulkCopy);
                buffer?.Dispose();
            }

            if (!result.TableCreated)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return result;
        }

        private async Task<TabularImportResult> ImportZippedCsvAsync(SqlConnection connection, TableImportTarget target, IReadOnlyList<IFormFile> files, string formatLabel, IReadOnlyList<TabularColumnDefinition> columns, int batchSize)
        {
            List<string>? headers = null;
            SqlBulkCopy? bulkCopy = null;
            DataTable? buffer = null;
            var result = new TabularImportResult();

            try
            {
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

                    var csvEntries = archive.Entries
                        .Where(entry => !string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (csvEntries.Count == 0)
                    {
                        throw new InvalidOperationException("Chaque fichier CSV.zip doit contenir au moins un fichier CSV.");
                    }

                    foreach (var entry in csvEntries)
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        using var parser = CreateCsvParser(reader);

                        var normalizedHeader = ReadNormalizedCsvHeader(parser);
                        if (normalizedHeader == null || normalizedHeader.Length == 0)
                        {
                            continue;
                        }

                        if (headers == null)
                        {
                            EnsureUniqueHeaders(normalizedHeader);
                            ValidateHeadersAgainstColumns(normalizedHeader, columns, formatLabel);
                            headers = normalizedHeader.ToList();
                            await CreateSqlTableAsync(connection, target, columns);
                            bulkCopy = CreateBulkCopy(connection, target, columns, batchSize);
                            buffer = CreateBufferTable(columns);
                            result.TableCreated = true;
                        }
                        else if (!HeadersMatch(headers, normalizedHeader))
                        {
                            throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                        }

                        if (bulkCopy != null && buffer != null)
                        {
                            result.RowsCopied += await BulkCopyCsvRowsAsync(bulkCopy, buffer, columns, parser, result.Errors, entry.FullName, batchSize);
                        }
                    }
                }
            }
            catch
            {
                if (result.TableCreated)
                {
                    await DropSqlTableIfExists(connection, target);
                }

                throw;
            }
            finally
            {
                DisposeBulkCopy(bulkCopy);
                buffer?.Dispose();
            }

            if (!result.TableCreated)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return result;
        }

        private async Task<TabularImportResult> ImportExcelAsync(SqlConnection connection, TableImportTarget target, IReadOnlyList<IFormFile> files, string formatLabel, IReadOnlyList<TabularColumnDefinition> columns, int batchSize)
        {
            List<string>? headers = null;
            SqlBulkCopy? bulkCopy = null;
            DataTable? buffer = null;
            var result = new TabularImportResult();

            try
            {
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();
                    using var reader = ExcelReaderFactory.CreateReader(stream);

                    do
                    {
                        var normalizedHeader = ReadNormalizedExcelHeader(reader);
                        if (normalizedHeader == null || normalizedHeader.Length == 0)
                        {
                            continue;
                        }

                        if (headers == null)
                        {
                            EnsureUniqueHeaders(normalizedHeader);
                            ValidateHeadersAgainstColumns(normalizedHeader, columns, formatLabel);
                            headers = normalizedHeader.ToList();
                            await CreateSqlTableAsync(connection, target, columns);
                            bulkCopy = CreateBulkCopy(connection, target, columns, batchSize);
                            buffer = CreateBufferTable(columns);
                            result.TableCreated = true;
                        }
                        else if (!HeadersMatch(headers, normalizedHeader))
                        {
                            throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                        }

                        if (bulkCopy != null && buffer != null)
                        {
                            result.RowsCopied += await BulkCopyExcelRowsAsync(bulkCopy, buffer, columns, reader, result.Errors, file.FileName, batchSize);
                        }
                    }
                    while (reader.NextResult());
                }
            }
            catch
            {
                if (result.TableCreated)
                {
                    await DropSqlTableIfExists(connection, target);
                }

                throw;
            }
            finally
            {
                DisposeBulkCopy(bulkCopy);
                buffer?.Dispose();
            }

            if (!result.TableCreated)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return result;
        }

        private async Task<TabularImportResult> ImportParquetAsync(SqlConnection connection, TableImportTarget target, IReadOnlyList<IFormFile> files, string formatLabel, IReadOnlyList<TabularColumnDefinition> columns, int batchSize)
        {
            List<string>? headers = null;
            SqlBulkCopy? bulkCopy = null;
            DataTable? buffer = null;
            var result = new TabularImportResult();

            try
            {
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();
                    using var reader = await ParquetReader.CreateAsync(stream);

                    var headerInfo = ReadNormalizedParquetHeader(reader);
                    if (headerInfo == null || headerInfo.Value.Headers.Length == 0)
                    {
                        continue;
                    }

                    var normalizedHeader = headerInfo.Value.Headers;

                    if (headers == null)
                    {
                        EnsureUniqueHeaders(normalizedHeader);
                        ValidateHeadersAgainstColumns(normalizedHeader, columns, formatLabel);
                        headers = normalizedHeader.ToList();
                        await CreateSqlTableAsync(connection, target, columns);
                        bulkCopy = CreateBulkCopy(connection, target, columns, batchSize);
                        buffer = CreateBufferTable(columns);
                        result.TableCreated = true;
                    }
                    else if (!HeadersMatch(headers, normalizedHeader))
                    {
                        throw new InvalidOperationException($"The {formatLabel} files must share the same columns.");
                    }

                    if (bulkCopy != null && buffer != null)
                    {
                        result.RowsCopied += await BulkCopyParquetRowsAsync(bulkCopy, buffer, columns, reader, headerInfo.Value.Fields, result.Errors, file.FileName, batchSize);
                    }
                }
            }
            catch
            {
                if (result.TableCreated)
                {
                    await DropSqlTableIfExists(connection, target);
                }

                throw;
            }
            finally
            {
                DisposeBulkCopy(bulkCopy);
                buffer?.Dispose();
            }

            if (!result.TableCreated)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }

            return result;
        }

        private static TextFieldParser CreateCsvParser(TextReader reader)
        {
            var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                TrimWhiteSpace = false,
                HasFieldsEnclosedInQuotes = true
            };

            parser.SetDelimiters(",");
            return parser;
        }

        private static string[]? ReadNormalizedCsvHeader(TextFieldParser parser)
        {
            while (!parser.EndOfData)
            {
                var candidate = parser.ReadFields();
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Length == 1 && string.IsNullOrWhiteSpace(candidate[0]))
                {
                    continue;
                }

                return candidate
                    .Select(h => (h ?? string.Empty).Trim())
                    .ToArray();
            }

            return null;
        }

        private static async Task<int> BulkCopyCsvRowsAsync(SqlBulkCopy bulkCopy, DataTable buffer, IReadOnlyList<TabularColumnDefinition> columns, TextFieldParser parser, List<TabularImportError> errors, string fileName, int batchSize)
        {
            var totalCopied = 0;
            var rowNumber = 0L;

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null)
                {
                    continue;
                }

                if (fields.Length == 1 && string.IsNullOrWhiteSpace(fields[0]))
                {
                    continue;
                }

                if (fields.Length != columns.Count)
                {
                    throw new InvalidOperationException("A row in the CSV file does not match the number of header columns.");
                }

                var rowValues = new object[columns.Count];
                var hasValue = false;
                rowNumber++;

                for (var i = 0; i < fields.Length; i++)
                {
                    var converted = ConvertValue(fields[i], columns[i], errors, fileName, rowNumber);
                    if (converted != DBNull.Value)
                    {
                        hasValue = true;
                    }

                    rowValues[i] = converted ?? DBNull.Value;
                }

                if (!hasValue)
                {
                    continue;
                }

                buffer.Rows.Add(rowValues);

                if (buffer.Rows.Count >= batchSize)
                {
                    await bulkCopy.WriteToServerAsync(buffer);
                    totalCopied += buffer.Rows.Count;
                    buffer.Clear();
                }
            }

            if (buffer.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(buffer);
                totalCopied += buffer.Rows.Count;
                buffer.Clear();
            }

            return totalCopied;
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

        private static string[]? ReadNormalizedExcelHeader(IExcelDataReader reader)
        {
            while (reader.Read())
            {
                var fieldCount = reader.FieldCount;
                if (fieldCount == 0)
                {
                    continue;
                }

                var values = new string[fieldCount];
                var hasValue = false;

                for (var i = 0; i < fieldCount; i++)
                {
                    var cell = reader.GetValue(i)?.ToString() ?? string.Empty;
                    var trimmed = cell.Trim();
                    values[i] = trimmed;
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        hasValue = true;
                    }
                }

                if (!hasValue)
                {
                    continue;
                }

                return values;
            }

            return null;
        }

        private static async Task<int> BulkCopyParquetRowsAsync(SqlBulkCopy bulkCopy, DataTable buffer, IReadOnlyList<TabularColumnDefinition> columns, ParquetReader reader, IReadOnlyList<DataField> fields, List<TabularImportError> errors, string fileName, int batchSize)
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
                        await bulkCopy.WriteToServerAsync(buffer);
                        totalCopied += buffer.Rows.Count;
                        buffer.Clear();
                    }
                }
            }

            if (buffer.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(buffer);
                totalCopied += buffer.Rows.Count;
                buffer.Clear();
            }

            return totalCopied;
        }

        private static async Task<int> BulkCopyExcelRowsAsync(SqlBulkCopy bulkCopy, DataTable buffer, IReadOnlyList<TabularColumnDefinition> columns, IExcelDataReader reader, List<TabularImportError> errors, string fileName, int batchSize)
        {
            var totalCopied = 0;
            var rowNumber = 0L;

            while (reader.Read())
            {
                if (reader.FieldCount > columns.Count)
                {
                    ValidateExtraExcelColumns(reader, columns.Count);
                }

                var rowValues = new object[columns.Count];
                var hasValue = false;
                rowNumber++;

                for (var i = 0; i < columns.Count; i++)
                {
                    object? cell = i < reader.FieldCount ? reader.GetValue(i) : null;
                    var converted = ConvertValue(cell, columns[i], errors, fileName, rowNumber);
                    if (converted != DBNull.Value)
                    {
                        hasValue = true;
                    }

                    rowValues[i] = converted ?? DBNull.Value;
                }

                if (!hasValue)
                {
                    continue;
                }

                buffer.Rows.Add(rowValues);

                if (buffer.Rows.Count >= batchSize)
                {
                    await bulkCopy.WriteToServerAsync(buffer);
                    totalCopied += buffer.Rows.Count;
                    buffer.Clear();
                }
            }

            if (buffer.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(buffer);
                totalCopied += buffer.Rows.Count;
                buffer.Clear();
            }

            return totalCopied;
        }

        private static void ValidateExtraExcelColumns(IExcelDataReader reader, int expectedColumnCount)
        {
            for (var i = expectedColumnCount; i < reader.FieldCount; i++)
            {
                var extra = reader.GetValue(i);
                if (extra != null && !string.IsNullOrWhiteSpace(Convert.ToString(extra, CultureInfo.InvariantCulture)))
                {
                    throw new InvalidOperationException("A row in the XLSX file contains more columns than the header.");
                }
            }
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

        private static bool HeadersMatch(IReadOnlyList<string> expected, IReadOnlyList<string> candidate)
        {
            if (expected.Count != candidate.Count)
            {
                return false;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i], candidate[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
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

        private static async Task CreateSqlTableAsync(SqlConnection connection, TableImportTarget target, IReadOnlyList<TabularColumnDefinition> columns)
        {
            var columnDefs = string.Join(", ", columns.Select(c =>
            {
                var safeColumn = c.Name.Replace("]", "]]", StringComparison.Ordinal);
                return $"[{safeColumn}] {GetSqlType(c)}";
            }));

            var hasIdColumn = columns.Any(h => string.Equals(h.Name, "id", StringComparison.OrdinalIgnoreCase));

            var createTableCommand = hasIdColumn
                ? $"CREATE TABLE {target.QualifiedNameWithDatabase} ({columnDefs})"
                : $"CREATE TABLE {target.QualifiedNameWithDatabase} (Id INT IDENTITY(1,1) PRIMARY KEY, {columnDefs})";

            using var cmd = new SqlCommand(createTableCommand, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string GetSqlType(TabularColumnDefinition column)
        {
            return column.ColumnType switch
            {
                TabularColumnType.Bit => "BIT",
                TabularColumnType.Int => "INT",
                TabularColumnType.BigInt => "BIGINT",
                TabularColumnType.Decimal => "DECIMAL(38, 12)",
                TabularColumnType.Float => "FLOAT",
                TabularColumnType.DateTime2 => "DATETIME2",
                TabularColumnType.NVarChar => BuildNVarCharType(column.MaxLength),
                _ => "NVARCHAR(MAX)"
            };
        }

        private static string BuildNVarCharType(int? maxLength)
        {
            if (!maxLength.HasValue || maxLength.Value <= 0)
            {
                return $"NVARCHAR({DefaultNVarCharLength})";
            }

            var boundedLength = Math.Min(maxLength.Value, MaxInlineNVarCharLength);
            var sqlLength = maxLength.Value > MaxInlineNVarCharLength ? "MAX" : boundedLength.ToString(CultureInfo.InvariantCulture);
            return $"NVARCHAR({sqlLength})";
        }

        private static async Task DropSqlTableIfExists(SqlConnection connection, TableImportTarget target)
        {
            var qualifiedName = target.QualifiedNameWithDatabase;
            var dropCommand = $"IF OBJECT_ID('{target.ObjectIdLiteral}', 'U') IS NOT NULL DROP TABLE {qualifiedName};";

            using var cmd = new SqlCommand(dropCommand, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static void DisposeBulkCopy(SqlBulkCopy? bulkCopy)
        {
            if (bulkCopy == null)
            {
                return;
            }

            bulkCopy.Close();
            if (bulkCopy is IDisposable disposable)
            {
                disposable.Dispose();
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

            if (column.ColumnType == TabularColumnType.Decimal && rawValue is decimal decimalValue)
            {
                return decimalValue;
            }

            var fallback = Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fallback))
            {
                return DBNull.Value;
            }

            return ConvertFromString(fallback.Trim(), column, errors, fileName, rowNumber);
        }

        private static object ConvertFromString(string value, TabularColumnDefinition column, List<TabularImportError> errors, string fileName, long rowNumber)
        {
            switch (column.ColumnType)
            {
                case TabularColumnType.Bit:
                    if (TryParseBooleanCandidate(value, out var parsedBool))
                    {
                        return parsedBool;
                    }

                    LogError(errors, new TabularImportError(fileName, rowNumber, column.Name, value, "Cannot convert value to bit; stored as NULL."));
                    return DBNull.Value;
                case TabularColumnType.Int:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                    {
                        return parsedInt;
                    }

                    LogError(errors, new TabularImportError(fileName, rowNumber, column.Name, value, "Cannot convert value to int; stored as NULL."));
                    return DBNull.Value;
                case TabularColumnType.BigInt:
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
                    {
                        return parsedLong;
                    }

                    LogError(errors, new TabularImportError(fileName, rowNumber, column.Name, value, "Cannot convert value to bigint; stored as NULL."));
                    return DBNull.Value;
                case TabularColumnType.Decimal:
                    if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDecimal) ||
                        decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedDecimal))
                    {
                        return parsedDecimal;
                    }

                    LogError(errors, new TabularImportError(fileName, rowNumber, column.Name, value, "Cannot convert value to decimal; stored as NULL."));
                    return DBNull.Value;
                case TabularColumnType.Float:
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble) ||
                        double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedDouble))
                    {
                        return parsedDouble;
                    }

                    LogError(errors, new TabularImportError(fileName, rowNumber, column.Name, value, "Cannot convert value to float; stored as NULL."));
                    return DBNull.Value;
                case TabularColumnType.DateTime2:
                    if (TryParseDateTimeValue(value, out var parsedDate))
                    {
                        return parsedDate;
                    }

                    LogError(errors, new TabularImportError(fileName, rowNumber, column.Name, value, "Cannot convert value to datetime2; stored as NULL."));
                    return DBNull.Value;
                case TabularColumnType.NVarChar:
                default:
                    return value;
            }
        }

        private static void LogError(List<TabularImportError> errors, TabularImportError error)
        {
            if (errors.Count >= MaxLoggedErrors)
            {
                return;
            }

            errors.Add(error);
        }
    }
}