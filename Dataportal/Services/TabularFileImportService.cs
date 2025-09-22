using Dataportal.Context;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Dataportal.Services
{
    public class TabularFileImportService : ITabularFileImporter
    {
        private readonly string _connectionString;
        private static readonly object EncodingLock = new();
        private static bool _encodingRegistered;

        public TabularFileImportService(ApplicationDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("La chaîne de connexion de la base de données est introuvable.");

            EnsureEncodingProviderRegistered();
        }

        public async Task ImportAsync(string tableName, IEnumerable<IFormFile> files)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Le nom de la table est requis.", nameof(tableName));
            }

            ArgumentNullException.ThrowIfNull(files);

            var fileList = files
                .Where(f => f != null)
                .ToList();

            if (fileList.Count == 0)
            {
                throw new InvalidOperationException("Les fichiers fournis sont vides.");
            }

            var extension = Path.GetExtension(fileList[0].FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new InvalidOperationException("Impossible de déterminer le type des fichiers importés.");
            }

            extension = extension.ToLowerInvariant();

            if (fileList.Any(f => !string.Equals(Path.GetExtension(f.FileName), extension, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Tous les fichiers importés pour cette étape doivent être du même type (CSV ou XLSX).");
            }

            var formatLabel = extension switch
            {
                ".csv" => "CSV",
                ".xlsx" => "XLSX",
                _ => "importés"
            };

            switch (extension)
            {
                case ".csv":
                    await ImportCsvAsync(tableName, fileList, formatLabel);
                    break;
                case ".xlsx":
                    await ImportExcelAsync(tableName, fileList, formatLabel);
                    break;
                default:
                    throw new InvalidOperationException("Seuls les fichiers CSV ou XLSX sont pris en charge pour le moment.");
            }
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

        private async Task ImportCsvAsync(string tableName, IReadOnlyList<IFormFile> files, string formatLabel)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            List<string>? headers = null;
            SqlBulkCopy? bulkCopy = null;
            DataTable? buffer = null;
            var tableCreated = false;

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

                    if (normalizedHeader.Any(string.IsNullOrWhiteSpace))
                    {
                        throw new InvalidOperationException("Les en-têtes de colonnes ne peuvent pas être vides.");
                    }

                    if (headers == null)
                    {
                        EnsureUniqueHeaders(normalizedHeader);
                        headers = normalizedHeader.ToList();
                        await CreateSqlTableAsync(connection, tableName, headers);
                        bulkCopy = CreateBulkCopy(connection, tableName, headers);
                        buffer = CreateBufferTable(headers);
                        tableCreated = true;
                    }
                    else if (!HeadersMatch(headers, normalizedHeader))
                    {
                        throw new InvalidOperationException($"Les fichiers {formatLabel} doivent avoir les mêmes colonnes.");
                    }

                    if (bulkCopy != null && buffer != null)
                    {
                        await BulkCopyCsvRowsAsync(bulkCopy, buffer, headers, parser);
                    }
                }
            }
            catch
            {
                if (tableCreated)
                {
                    await DropSqlTableIfExists(connection, tableName);
                }

                throw;
            }
            finally
            {
                DisposeBulkCopy(bulkCopy);
                buffer?.Dispose();
            }

            if (!tableCreated)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }
        }

        private async Task ImportExcelAsync(string tableName, IReadOnlyList<IFormFile> files, string formatLabel)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            List<string>? headers = null;
            SqlBulkCopy? bulkCopy = null;
            DataTable? buffer = null;
            var tableCreated = false;

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

                        if (normalizedHeader.Any(string.IsNullOrWhiteSpace))
                        {
                            throw new InvalidOperationException("Les en-têtes de colonnes ne peuvent pas être vides.");
                        }

                        if (headers == null)
                        {
                            EnsureUniqueHeaders(normalizedHeader);
                            headers = normalizedHeader.ToList();
                            await CreateSqlTableAsync(connection, tableName, headers);
                            bulkCopy = CreateBulkCopy(connection, tableName, headers);
                            buffer = CreateBufferTable(headers);
                            tableCreated = true;
                        }
                        else if (!HeadersMatch(headers, normalizedHeader))
                        {
                            throw new InvalidOperationException($"Les fichiers {formatLabel} doivent avoir les mêmes colonnes.");
                        }

                        if (bulkCopy != null && buffer != null)
                        {
                            await BulkCopyExcelRowsAsync(bulkCopy, buffer, headers, reader);
                        }
                    }
                    while (reader.NextResult());
                }
            }
            catch
            {
                if (tableCreated)
                {
                    await DropSqlTableIfExists(connection, tableName);
                }

                throw;
            }
            finally
            {
                DisposeBulkCopy(bulkCopy);
                buffer?.Dispose();
            }

            if (!tableCreated)
            {
                throw new InvalidOperationException($"Les fichiers {formatLabel} fournis sont vides.");
            }
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

        private static async Task BulkCopyCsvRowsAsync(SqlBulkCopy bulkCopy, DataTable buffer, IReadOnlyList<string> headers, TextFieldParser parser)
        {
            const int batchSize = 5000;

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

                if (fields.Length != headers.Count)
                {
                    throw new InvalidOperationException("Une ligne du fichier CSV ne correspond pas au nombre de colonnes de l'en-tête.");
                }

                var rowValues = new object[headers.Count];
                for (var i = 0; i < fields.Length; i++)
                {
                    rowValues[i] = fields[i] ?? (object)DBNull.Value;
                }

                buffer.Rows.Add(rowValues);

                if (buffer.Rows.Count >= batchSize)
                {
                    await bulkCopy.WriteToServerAsync(buffer);
                    buffer.Clear();
                }
            }

            if (buffer.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(buffer);
                buffer.Clear();
            }
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

        private static async Task BulkCopyExcelRowsAsync(SqlBulkCopy bulkCopy, DataTable buffer, IReadOnlyList<string> headers, IExcelDataReader reader)
        {
            const int batchSize = 5000;

            while (reader.Read())
            {
                if (reader.FieldCount > headers.Count)
                {
                    ValidateExtraExcelColumns(reader, headers.Count);
                }

                var rowValues = new object[headers.Count];
                var hasValue = false;

                for (var i = 0; i < headers.Count; i++)
                {
                    object? cell = i < reader.FieldCount ? reader.GetValue(i) : null;
                    if (cell == null)
                    {
                        rowValues[i] = DBNull.Value;
                        continue;
                    }

                    if (cell is string stringValue)
                    {
                        if (string.IsNullOrWhiteSpace(stringValue))
                        {
                            rowValues[i] = DBNull.Value;
                            continue;
                        }

                        hasValue = true;
                        rowValues[i] = stringValue;
                        continue;
                    }

                    hasValue = true;
                    rowValues[i] = Convert.ToString(cell, CultureInfo.InvariantCulture);
                }

                if (!hasValue)
                {
                    continue;
                }

                buffer.Rows.Add(rowValues);

                if (buffer.Rows.Count >= batchSize)
                {
                    await bulkCopy.WriteToServerAsync(buffer);
                    buffer.Clear();
                }
            }

            if (buffer.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(buffer);
                buffer.Clear();
            }
        }

        private static void ValidateExtraExcelColumns(IExcelDataReader reader, int expectedColumnCount)
        {
            for (var i = expectedColumnCount; i < reader.FieldCount; i++)
            {
                var extra = reader.GetValue(i);
                if (extra != null && !string.IsNullOrWhiteSpace(Convert.ToString(extra, CultureInfo.InvariantCulture)))
                {
                    throw new InvalidOperationException("Une ligne du fichier XLSX contient plus de colonnes que l'en-tête.");
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
                throw new InvalidOperationException("Les en-têtes de colonnes doivent être uniques.");
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

        private static DataTable CreateBufferTable(IReadOnlyList<string> headers)
        {
            var table = new DataTable();
            foreach (var header in headers)
            {
                var column = table.Columns.Add(header, typeof(string));
                column.AllowDBNull = true;
            }

            return table;
        }

        private static SqlBulkCopy CreateBulkCopy(SqlConnection connection, string tableName, IReadOnlyList<string> headers)
        {
            var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = GetQualifiedTableName(tableName),
                BulkCopyTimeout = 0,
                BatchSize = 5000
            };

            foreach (var header in headers)
            {
                bulkCopy.ColumnMappings.Add(header, header);
            }

            return bulkCopy;
        }

        private static async Task CreateSqlTableAsync(SqlConnection connection, string tableName, IReadOnlyList<string> headers)
        {
            var columnDefs = string.Join(", ", headers.Select(h =>
            {
                var safeColumn = h.Replace("]", "]]");
                return $"[{safeColumn}] NVARCHAR(MAX)";
            }));

            var hasIdColumn = headers.Any(h => string.Equals(h, "id", StringComparison.OrdinalIgnoreCase));

            var createTableCommand = hasIdColumn
                ? $"CREATE TABLE {GetQualifiedTableName(tableName)} ({columnDefs})"
                : $"CREATE TABLE {GetQualifiedTableName(tableName)} (Id INT IDENTITY(1,1) PRIMARY KEY, {columnDefs})";

            using var cmd = new SqlCommand(createTableCommand, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string GetQualifiedTableName(string tableName)
        {
            var safeName = tableName.Replace("]", "]]");
            return $"[DataPortal].[dbo].[{safeName}]";
        }

        private static async Task DropSqlTableIfExists(SqlConnection connection, string tableName)
        {
            var qualifiedName = GetQualifiedTableName(tableName);
            var objectIdName = qualifiedName.Replace("'", "''");
            var dropCommand = $"IF OBJECT_ID('{objectIdName}', 'U') IS NOT NULL DROP TABLE {qualifiedName};";

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
    }
}