using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Controllers.NotebookApi
{
    [ApiController]
    public abstract class NotebookApiBaseController : ControllerBase
    {
        protected enum NotebookApiTableType
        {
            Donnees,
            EventLogs,
            ContexteEnvironnemental
        }

        private static readonly Regex TableNameRegex = new Regex("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
        private static readonly Regex ColumnNameRegex = new Regex("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

        protected NotebookApiBaseController(ApplicationDbContext context, IOptions<NotebookApiOptions> options)
        {
            Context = context;
            Options = options.Value;
        }

        protected ApplicationDbContext Context { get; }

        protected NotebookApiOptions Options { get; }

        protected async Task<Metadonnee?> FindMetadonneeByIdAsync(int datasetId, CancellationToken cancellationToken)
        {
            return await Context.Metadonnee
                .AsNoTracking()
                .Include(m => m.Utilisateur)
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m => m.Id == datasetId, cancellationToken);
        }

        protected async Task<Metadonnee?> FindMetadonneeByTableNameAsync(
            NotebookApiTableType tableType,
            string tableName,
            CancellationToken cancellationToken)
        {
            var schemaName = GetSchemaName(tableType);
            var qualifiedName = schemaName == null ? null : $"{schemaName}.{tableName}";
            var query = Context.Metadonnee
                .AsNoTracking()
                .Include(m => m.Utilisateur)
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental);

            return tableType switch
            {
                NotebookApiTableType.Donnees => await query.FirstOrDefaultAsync(
                    m => m.Donnees != null &&
                         (m.Donnees.NomDeLaTable == tableName || m.Donnees.NomDeLaTable == qualifiedName),
                    cancellationToken),
                NotebookApiTableType.EventLogs => await query.FirstOrDefaultAsync(
                    m => m.DonneesEventLogs != null &&
                         (m.DonneesEventLogs.NomDeLaTable == tableName ||
                          m.DonneesEventLogs.NomDeLaTable == qualifiedName),
                    cancellationToken),
                NotebookApiTableType.ContexteEnvironnemental => await query.FirstOrDefaultAsync(
                    m => m.DonneesContexteEnvironnemental != null &&
                         (m.DonneesContexteEnvironnemental.NomDeLaTable == tableName ||
                          m.DonneesContexteEnvironnemental.NomDeLaTable == qualifiedName),
                    cancellationToken),
                _ => null
            };
        }

        protected bool TryResolveDatasetTarget(Metadonnee metadonnee, out TableImportTarget target, out string? errorMessage)
        {
            errorMessage = null;
            target = null!;

            if (!string.IsNullOrWhiteSpace(metadonnee.Donnees?.NomDeLaTable))
            {
                if (TryBuildTableImportTarget(metadonnee.Donnees.NomDeLaTable, TableImportSchemas.Donnees, out var parsedTarget))
                {
                    target = parsedTarget;
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(metadonnee.DonneesEventLogs?.NomDeLaTable))
            {
                if (TryBuildTableImportTarget(
                    metadonnee.DonneesEventLogs.NomDeLaTable,
                    TableImportSchemas.DonneesEventLogs,
                    out var parsedTarget))
                {
                    target = parsedTarget;
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(metadonnee.DonneesContexteEnvironnemental?.NomDeLaTable))
            {
                if (TryBuildTableImportTarget(
                    metadonnee.DonneesContexteEnvironnemental.NomDeLaTable,
                    TableImportSchemas.DonneesContexteEnvironnemental,
                    out var parsedTarget))
                {
                    target = parsedTarget;
                    return true;
                }
            }

            errorMessage = "Dataset does not include a data, event logs, or environmental context table.";
            return false;
        }

        protected bool TryResolveDatasetTarget(
            Metadonnee metadonnee,
            NotebookApiTableType tableType,
            out TableImportTarget target,
            out string? errorMessage)
        {
            errorMessage = null;
            target = null!;

            switch (tableType)
            {
                case NotebookApiTableType.Donnees:
                    if (!string.IsNullOrWhiteSpace(metadonnee.Donnees?.NomDeLaTable))
                    {
                        if (TryBuildTableImportTarget(metadonnee.Donnees.NomDeLaTable, TableImportSchemas.Donnees, out var parsedTarget))
                        {
                            target = parsedTarget;
                            return true;
                        }
                    }
                    break;
                case NotebookApiTableType.EventLogs:
                    if (!string.IsNullOrWhiteSpace(metadonnee.DonneesEventLogs?.NomDeLaTable))
                    {
                        if (TryBuildTableImportTarget(
                            metadonnee.DonneesEventLogs.NomDeLaTable,
                            TableImportSchemas.DonneesEventLogs,
                            out var parsedTarget))
                        {
                            target = parsedTarget;
                            return true;
                        }
                    }
                    break;
                case NotebookApiTableType.ContexteEnvironnemental:
                    if (!string.IsNullOrWhiteSpace(metadonnee.DonneesContexteEnvironnemental?.NomDeLaTable))
                    {
                        if (TryBuildTableImportTarget(
                            metadonnee.DonneesContexteEnvironnemental.NomDeLaTable,
                            TableImportSchemas.DonneesContexteEnvironnemental,
                            out var parsedTarget))
                        {
                            target = parsedTarget;
                            return true;
                        }
                    }
                    break;
            }

            errorMessage = "Dataset does not include the requested table type.";
            return false;
        }

        protected bool TryParseTableType(string? tableType, out NotebookApiTableType parsedType)
        {
            parsedType = default;
            if (string.IsNullOrWhiteSpace(tableType))
            {
                return false;
            }

            var normalized = new string(tableType.Where(char.IsLetterOrDigit).ToArray())
                .ToLowerInvariant();

            return normalized switch
            {
                "donnees" => SetParsedType(NotebookApiTableType.Donnees, out parsedType),
                "eventlogs" => SetParsedType(NotebookApiTableType.EventLogs, out parsedType),
                "donneeseventlogs" => SetParsedType(NotebookApiTableType.EventLogs, out parsedType),
                "contexteenv" => SetParsedType(NotebookApiTableType.ContexteEnvironnemental, out parsedType),
                "contexteenvironmental" => SetParsedType(NotebookApiTableType.ContexteEnvironnemental, out parsedType),
                "donneescontexteenv" => SetParsedType(NotebookApiTableType.ContexteEnvironnemental, out parsedType),
                "donneescontexteenvironmental" => SetParsedType(NotebookApiTableType.ContexteEnvironnemental, out parsedType),
                _ => false
            };
        }

        protected bool IsValidTableName(string tableName)
        {
            return TableNameRegex.IsMatch(tableName);
        }

        protected bool IsValidColumnName(string columnName)
        {
            return ColumnNameRegex.IsMatch(columnName);
        }

        private static string? GetSchemaName(NotebookApiTableType tableType)
        {
            return tableType switch
            {
                NotebookApiTableType.Donnees => TableImportSchemas.Donnees,
                NotebookApiTableType.EventLogs => TableImportSchemas.DonneesEventLogs,
                NotebookApiTableType.ContexteEnvironnemental => TableImportSchemas.DonneesContexteEnvironnemental,
                _ => null
            };
        }

        private static bool TryBuildTableImportTarget(string? storedName, string fallbackSchema, out TableImportTarget? target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(storedName))
            {
                return false;
            }

            var segments = storedName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2 && IsKnownSchema(segments[0]))
            {
                target = new TableImportTarget(segments[0], segments[1]);
                return true;
            }

            target = new TableImportTarget(fallbackSchema, storedName.Trim());
            return true;
        }

        private static bool IsKnownSchema(string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return false;
            }

            return schema.Equals(TableImportSchemas.Donnees, StringComparison.OrdinalIgnoreCase) ||
                   schema.Equals(TableImportSchemas.DonneesEventLogs, StringComparison.OrdinalIgnoreCase) ||
                   schema.Equals(TableImportSchemas.DonneesContexteEnvironnemental, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SetParsedType(NotebookApiTableType tableType, out NotebookApiTableType parsedType)
        {
            parsedType = tableType;
            return true;
        }

        protected async Task<IReadOnlyList<PrimaryKeyColumn>> GetPrimaryKeyColumnsAsync(TableImportTarget target, CancellationToken cancellationToken)
        {
            var columns = new List<PrimaryKeyColumn>();
            var query = @"SELECT c.name, ic.key_ordinal, t.name AS type_name, c.is_nullable
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables tbl ON i.object_id = tbl.object_id
INNER JOIN sys.schemas s ON tbl.schema_id = s.schema_id
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE i.is_primary_key = 1 AND s.name = @schema AND tbl.name = @table
ORDER BY ic.key_ordinal";

            using var connection = new SqlConnection(Context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@schema", target.Schema);
            command.Parameters.AddWithValue("@table", target.TableName);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var typeName = reader.GetString(2);
                var isNullable = reader.GetBoolean(3);
                var dbType = MapSqlType(typeName);
                columns.Add(new PrimaryKeyColumn(name, dbType, isNullable));
            }

            return columns;
        }

        protected async Task<SqlDbType?> GetColumnTypeAsync(TableImportTarget target, string columnName, CancellationToken cancellationToken)
        {
            var query = @"SELECT t.name
FROM sys.columns c
INNER JOIN sys.tables tbl ON c.object_id = tbl.object_id
INNER JOIN sys.schemas s ON tbl.schema_id = s.schema_id
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE s.name = @schema AND tbl.name = @table AND c.name = @column";

            await using var connection = new SqlConnection(Context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@schema", target.Schema);
            command.Parameters.AddWithValue("@table", target.TableName);
            command.Parameters.AddWithValue("@column", columnName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            return MapSqlType(result.ToString() ?? string.Empty);
        }

        protected static string BuildOrderByClause(IReadOnlyList<PrimaryKeyColumn> columns)
        {
            return string.Join(", ", columns.Select(column => $"{EscapeColumn(column.Name)} ASC"));
        }

        protected static string BuildCursorPredicate(IReadOnlyList<PrimaryKeyColumn> columns)
        {
            var conditions = new List<string>();
            for (var i = 0; i < columns.Count; i++)
            {
                var parts = new List<string>();
                for (var j = 0; j < i; j++)
                {
                    parts.Add($"{EscapeColumn(columns[j].Name)} = @pk{j}");
                }

                parts.Add($"{EscapeColumn(columns[i].Name)} > @pk{i}");
                conditions.Add($"({string.Join(" AND ", parts)})");
            }

            return string.Join(" OR ", conditions);
        }

        protected static string EncodeCursor(IReadOnlyList<object?> values)
        {
            var json = JsonSerializer.Serialize(values);
            var bytes = Encoding.UTF8.GetBytes(json);
            return Base64UrlEncode(bytes);
        }

        protected static object?[] DecodeCursor(string cursor, IReadOnlyList<PrimaryKeyColumn> columns)
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(cursor));
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Cursor payload must be an array.");
            }

            var elements = document.RootElement.EnumerateArray().ToList();
            if (elements.Count != columns.Count)
            {
                throw new InvalidOperationException("Cursor value count does not match primary key columns.");
            }

            var values = new object?[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                values[i] = ParseCursorValue(elements[i], columns[i]);
            }

            return values;
        }

        protected static Parquet.Schema.ParquetSchema BuildParquetSchema(SqlDataReader reader)
        {
            var fields = new List<Parquet.Schema.Field>();
            var schemaTable = reader.GetSchemaTable();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var fieldType = reader.GetFieldType(i);
                var isNullable = true;

                if (schemaTable != null)
                {
                    var row = schemaTable.Rows[i];
                    if (row["AllowDBNull"] is bool allowNull)
                    {
                        isNullable = allowNull;
                    }
                }

                var baseType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
                fields.Add(new Parquet.Schema.DataField(columnName, baseType, isNullable));
            }

            return new Parquet.Schema.ParquetSchema(fields);
        }

        protected static string EscapeColumn(string columnName)
        {
            return $"[{columnName.Replace("]", "]]", StringComparison.Ordinal)}]";
        }

        protected static SqlDbType MapSqlType(string typeName)
        {
            return typeName.ToLowerInvariant() switch
            {
                "bigint" => SqlDbType.BigInt,
                "int" => SqlDbType.Int,
                "smallint" => SqlDbType.SmallInt,
                "tinyint" => SqlDbType.TinyInt,
                "bit" => SqlDbType.Bit,
                "decimal" => SqlDbType.Decimal,
                "numeric" => SqlDbType.Decimal,
                "money" => SqlDbType.Money,
                "smallmoney" => SqlDbType.SmallMoney,
                "float" => SqlDbType.Float,
                "real" => SqlDbType.Real,
                "date" => SqlDbType.Date,
                "datetime" => SqlDbType.DateTime,
                "datetime2" => SqlDbType.DateTime2,
                "datetimeoffset" => SqlDbType.DateTimeOffset,
                "smalldatetime" => SqlDbType.SmallDateTime,
                "time" => SqlDbType.Time,
                "uniqueidentifier" => SqlDbType.UniqueIdentifier,
                "char" => SqlDbType.Char,
                "nchar" => SqlDbType.NChar,
                "varchar" => SqlDbType.VarChar,
                "nvarchar" => SqlDbType.NVarChar,
                "text" => SqlDbType.Text,
                "ntext" => SqlDbType.NText,
                "binary" => SqlDbType.Binary,
                "varbinary" => SqlDbType.VarBinary,
                _ => SqlDbType.Variant
            };
        }

        protected static object? ParseCursorValue(JsonElement element, PrimaryKeyColumn column)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                if (column.IsNullable)
                {
                    return null;
                }

                throw new InvalidOperationException("Cursor contains null for non-nullable column.");
            }

            return column.DbType switch
            {
                SqlDbType.BigInt => element.GetInt64(),
                SqlDbType.Int => element.GetInt32(),
                SqlDbType.SmallInt => element.GetInt16(),
                SqlDbType.TinyInt => element.GetByte(),
                SqlDbType.Bit => element.GetBoolean(),
                SqlDbType.Decimal => element.GetDecimal(),
                SqlDbType.Money => element.GetDecimal(),
                SqlDbType.SmallMoney => element.GetDecimal(),
                SqlDbType.Float => element.GetDouble(),
                SqlDbType.Real => element.GetSingle(),
                SqlDbType.Date => element.GetDateTime(),
                SqlDbType.DateTime => element.GetDateTime(),
                SqlDbType.DateTime2 => element.GetDateTime(),
                SqlDbType.SmallDateTime => element.GetDateTime(),
                SqlDbType.DateTimeOffset => element.GetDateTimeOffset(),
                SqlDbType.Time => TimeSpan.Parse(element.GetString() ?? string.Empty),
                SqlDbType.UniqueIdentifier => Guid.Parse(element.GetString() ?? string.Empty),
                SqlDbType.Binary => Convert.FromBase64String(element.GetString() ?? string.Empty),
                SqlDbType.VarBinary => Convert.FromBase64String(element.GetString() ?? string.Empty),
                _ => element.GetString()
            };
        }

        protected static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        protected static byte[] Base64UrlDecode(string input)
        {
            var base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }

            return Convert.FromBase64String(base64);
        }

        protected sealed record PrimaryKeyColumn(string Name, SqlDbType DbType, bool IsNullable);

        protected sealed record NotebookApiAccessContext(int DatasetId, int? UserId, int? NotebookTokenId);

        protected sealed record NotebookApiRateLimitContext(string Key, long Limit, DateTime WindowEndUtc);

        protected NotebookApiAccessContext BuildAccessContext(int datasetId)
        {
            return new NotebookApiAccessContext(
                datasetId,
                HttpContextUserHelper.TryGetCurrentUserId(User),
                TryGetNotebookTokenId());
        }

        protected NotebookApiRateLimitContext? BuildPublicRateLimitContext()
        {
            if (Options.PublicDailyByteLimit <= 0)
            {
                return null;
            }

            var today = DateTime.UtcNow.Date;
            var windowEnd = today.AddDays(1);
            var key = $"notebook-public:{today:yyyyMMdd}";
            return new NotebookApiRateLimitContext(key, Options.PublicDailyByteLimit, windowEnd);
        }

        protected NotebookApiRateLimitContext? BuildPrivateTokenRateLimitContext(int tokenId)
        {
            if (Options.PrivateTokenByteLimit <= 0)
            {
                return null;
            }

            var windowMinutes = Options.PrivateTokenWindowMinutes <= 0 ? 60 : Options.PrivateTokenWindowMinutes;
            var windowEnd = DateTime.UtcNow.AddMinutes(windowMinutes);
            var key = $"notebook-private:{tokenId}";
            return new NotebookApiRateLimitContext(key, Options.PrivateTokenByteLimit, windowEnd);
        }

        protected async Task<IActionResult?> EnforceRateLimitAsync(NotebookApiRateLimitContext? rateLimitContext, CancellationToken cancellationToken)
        {
            if (rateLimitContext == null)
            {
                return null;
            }

            var normalizedKey = rateLimitContext.Key.ToLowerInvariant();
            var now = DateTime.UtcNow;
            var entry = await Context.RateLimitEntries.FirstOrDefaultAsync(r => r.Key == normalizedKey, cancellationToken);
            if (entry == null || entry.WindowEndUtc < now)
            {
                if (entry == null)
                {
                    entry = new RateLimitEntry
                    {
                        Key = normalizedKey,
                        Count = 0,
                        WindowEndUtc = rateLimitContext.WindowEndUtc
                    };
                    Context.RateLimitEntries.Add(entry);
                }
                else
                {
                    entry.Count = 0;
                    entry.WindowEndUtc = rateLimitContext.WindowEndUtc;
                    Context.RateLimitEntries.Update(entry);
                }

                await Context.SaveChangesAsync(cancellationToken);
                return null;
            }

            if (entry.Count >= rateLimitContext.Limit)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
                {
                    Title = "Rate limit exceeded",
                    Detail = "The notebook API rate limit has been exceeded. Try again later.",
                    Status = StatusCodes.Status429TooManyRequests,
                    Type = "https://httpstatuses.com/429"
                });
            }

            return null;
        }

        protected async Task<IActionResult> StreamParquetAsync(
            TableImportTarget target,
            IReadOnlyList<PrimaryKeyColumn> primaryKeyColumns,
            int limit,
            object?[]? cursorValues,
            NotebookApiAccessContext? accessContext,
            NotebookApiRateLimitContext? rateLimitContext,
            CancellationToken cancellationToken)
        {
            var qualifiedTable = target.QualifiedNameWithDatabase;
            var orderByClause = BuildOrderByClause(primaryKeyColumns);
            var cursorPredicate = cursorValues != null ? BuildCursorPredicate(primaryKeyColumns) : null;
            var whereClause = string.IsNullOrWhiteSpace(cursorPredicate) ? string.Empty : $"WHERE {cursorPredicate}";

            var limitPlusOne = limit + 1;
            var pkSelectList = string.Join(", ", primaryKeyColumns.Select(column => EscapeColumn(column.Name)));
            var pkQuery = $"SELECT TOP (@limitPlusOne) {pkSelectList} FROM {qualifiedTable} {whereClause} ORDER BY {orderByClause}";

            var pkRows = new List<object?[]>();

            await using (var connection = new SqlConnection(Context.Database.GetConnectionString()))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = new SqlCommand(pkQuery, connection))
                {
                    command.CommandTimeout = Options.CommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@limitPlusOne", limitPlusOne);
                    AddCursorParameters(command, primaryKeyColumns, cursorValues);

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var values = new object?[primaryKeyColumns.Count];
                        for (var i = 0; i < primaryKeyColumns.Count; i++)
                        {
                            values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }

                        pkRows.Add(values);
                    }
                }
            }

            if (pkRows.Count == 0)
            {
                await RecordAccessLogAsync(accessContext, 0, cancellationToken);
                await UpdateRateLimitAsync(rateLimitContext, 0, cancellationToken);
                return NoContent();
            }

            var rowCount = Math.Min(limit, pkRows.Count);
            var hasMore = pkRows.Count > limit;
            var nextCursor = EncodeCursor(pkRows[rowCount - 1]);

            Response.Headers["X-Row-Count"] = rowCount.ToString();
            Response.Headers["X-Has-More"] = hasMore ? "true" : "false";
            Response.Headers["X-Next-Cursor"] = hasMore ? nextCursor : string.Empty;
            Response.ContentType = "application/x-parquet";

            var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIoFeature != null)
            {
                syncIoFeature.AllowSynchronousIO = true;
            }

            var bytesWritten = 0L;

            try
            {
                await using var connection = new SqlConnection(Context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(
                    $"SELECT TOP (@limit) * FROM {qualifiedTable} {whereClause} ORDER BY {orderByClause}",
                    connection);
                command.CommandTimeout = Options.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@limit", limit);
                AddCursorParameters(command, primaryKeyColumns, cursorValues);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    await RecordAccessLogAsync(accessContext, 0, cancellationToken);
                    await UpdateRateLimitAsync(rateLimitContext, 0, cancellationToken);
                    return NoContent();
                }

                var schema = BuildParquetSchema(reader);
                await using var limitedStream = new NotebookApiLimitedStream(Response.Body, Options.MaxBytesPerResponse);
                await using var parquetWriter = await ParquetWriter.CreateAsync(schema, limitedStream, cancellationToken: cancellationToken);

                var rowGroupSize = Math.Max(1, Options.RowGroupSize);
                var columns = BuildColumnBuffers(reader.FieldCount);
                var rowsWritten = 0;

                do
                {
                    if (rowsWritten >= rowCount)
                    {
                        break;
                    }

                    AppendRow(reader, columns);
                    rowsWritten++;

                    if (rowsWritten % rowGroupSize == 0)
                    {
                        await WriteRowGroupAsync(parquetWriter, schema, columns, cancellationToken);
                    }
                } while (await reader.ReadAsync(cancellationToken));

                if (columns.Any(column => column.Count > 0))
                {
                    await WriteRowGroupAsync(parquetWriter, schema, columns, cancellationToken);
                }

                bytesWritten = limitedStream.BytesWritten;
            }
            catch (NotebookApiResponseTooLargeException)
            {
                if (!Response.HasStarted)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
                    {
                        Title = "Response too large",
                        Detail = "Notebook API response exceeded the configured size limit.",
                        Status = StatusCodes.Status413PayloadTooLarge,
                        Type = "https://httpstatuses.com/413"
                    });
                }

                HttpContext.Abort();
                return new EmptyResult();
            }

            await RecordAccessLogAsync(accessContext, bytesWritten, cancellationToken);
            await UpdateRateLimitAsync(rateLimitContext, bytesWritten, cancellationToken);

            return new EmptyResult();
        }

        protected async Task<IActionResult> StreamParquetAsync(
            TableImportTarget target,
            string? whereClause,
            string? orderByClause,
            int limit,
            IReadOnlyList<SqlParameter> parameters,
            NotebookApiAccessContext? accessContext,
            NotebookApiRateLimitContext? rateLimitContext,
            CancellationToken cancellationToken)
        {
            var qualifiedTable = target.QualifiedNameWithDatabase;
            var normalizedWhere = string.IsNullOrWhiteSpace(whereClause) ? string.Empty : $"WHERE {whereClause}";
            var normalizedOrder = string.IsNullOrWhiteSpace(orderByClause) ? string.Empty : $"ORDER BY {orderByClause}";

            var limitPlusOne = limit + 1;
            var rowCountQuery = $"SELECT TOP (@limitPlusOne) 1 FROM {qualifiedTable} {normalizedWhere} {normalizedOrder}";
            var rowCount = 0;

            await using (var connection = new SqlConnection(Context.Database.GetConnectionString()))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(rowCountQuery, connection);
                command.CommandTimeout = Options.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@limitPlusOne", limitPlusOne);
                AddQueryParameters(command, parameters);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rowCount++;
                }
            }

            if (rowCount == 0)
            {
                await RecordAccessLogAsync(accessContext, 0, cancellationToken);
                await UpdateRateLimitAsync(rateLimitContext, 0, cancellationToken);
                return NoContent();
            }

            var limitedRowCount = Math.Min(limit, rowCount);
            var hasMore = rowCount > limit;

            Response.Headers["X-Row-Count"] = limitedRowCount.ToString();
            Response.Headers["X-Has-More"] = hasMore ? "true" : "false";
            Response.ContentType = "application/x-parquet";

            var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIoFeature != null)
            {
                syncIoFeature.AllowSynchronousIO = true;
            }

            var bytesWritten = 0L;

            try
            {
                await using var connection = new SqlConnection(Context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(
                    $"SELECT TOP (@limit) * FROM {qualifiedTable} {normalizedWhere} {normalizedOrder}",
                    connection);
                command.CommandTimeout = Options.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@limit", limit);
                AddQueryParameters(command, parameters);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    await RecordAccessLogAsync(accessContext, 0, cancellationToken);
                    await UpdateRateLimitAsync(rateLimitContext, 0, cancellationToken);
                    return NoContent();
                }

                var schema = BuildParquetSchema(reader);
                await using var limitedStream = new NotebookApiLimitedStream(Response.Body, Options.MaxBytesPerResponse);
                await using var parquetWriter = await ParquetWriter.CreateAsync(schema, limitedStream, cancellationToken: cancellationToken);

                var rowGroupSize = Math.Max(1, Options.RowGroupSize);
                var columns = BuildColumnBuffers(reader.FieldCount);
                var rowsWritten = 0;

                do
                {
                    if (rowsWritten >= limitedRowCount)
                    {
                        break;
                    }

                    AppendRow(reader, columns);
                    rowsWritten++;

                    if (rowsWritten % rowGroupSize == 0)
                    {
                        await WriteRowGroupAsync(parquetWriter, schema, columns, cancellationToken);
                    }
                } while (await reader.ReadAsync(cancellationToken));

                if (columns.Any(column => column.Count > 0))
                {
                    await WriteRowGroupAsync(parquetWriter, schema, columns, cancellationToken);
                }

                bytesWritten = limitedStream.BytesWritten;
            }
            catch (NotebookApiResponseTooLargeException)
            {
                if (!Response.HasStarted)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
                    {
                        Title = "Response too large",
                        Detail = "Notebook API response exceeded the configured size limit.",
                        Status = StatusCodes.Status413PayloadTooLarge,
                        Type = "https://httpstatuses.com/413"
                    });
                }

                HttpContext.Abort();
                return new EmptyResult();
            }

            await RecordAccessLogAsync(accessContext, bytesWritten, cancellationToken);
            await UpdateRateLimitAsync(rateLimitContext, bytesWritten, cancellationToken);

            return new EmptyResult();
        }

        protected async Task<IActionResult> StreamParquetAsync(
            TableImportTarget target,
            IReadOnlyList<PrimaryKeyColumn> primaryKeyColumns,
            string? whereClause,
            string? orderByClause,
            int limit,
            IReadOnlyList<SqlParameter> parameters,
            object?[]? cursorValues,
            IReadOnlyList<string>? selectColumns,
            NotebookApiAccessContext? accessContext,
            NotebookApiRateLimitContext? rateLimitContext,
            CancellationToken cancellationToken)
        {
            var qualifiedTable = target.QualifiedNameWithDatabase;
            var cursorPredicate = cursorValues != null ? BuildCursorPredicate(primaryKeyColumns) : null;
            var combinedWhere = CombineWhereClauses(whereClause, cursorPredicate);
            var normalizedWhere = string.IsNullOrWhiteSpace(combinedWhere) ? string.Empty : $"WHERE {combinedWhere}";
            var normalizedOrder = string.IsNullOrWhiteSpace(orderByClause) ? string.Empty : $"ORDER BY {orderByClause}";
            var selectList = BuildSelectList(selectColumns);

            var limitPlusOne = limit + 1;
            var pkSelectList = string.Join(", ", primaryKeyColumns.Select(column => EscapeColumn(column.Name)));
            var pkQuery = $"SELECT TOP (@limitPlusOne) {pkSelectList} FROM {qualifiedTable} {normalizedWhere} {normalizedOrder}";

            var pkRows = new List<object?[]>();

            await using (var connection = new SqlConnection(Context.Database.GetConnectionString()))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(pkQuery, connection);
                command.CommandTimeout = Options.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@limitPlusOne", limitPlusOne);
                AddQueryParameters(command, parameters);
                AddCursorParameters(command, primaryKeyColumns, cursorValues);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var values = new object?[primaryKeyColumns.Count];
                    for (var i = 0; i < primaryKeyColumns.Count; i++)
                    {
                        values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }

                    pkRows.Add(values);
                }
            }

            if (pkRows.Count == 0)
            {
                await RecordAccessLogAsync(accessContext, 0, cancellationToken);
                await UpdateRateLimitAsync(rateLimitContext, 0, cancellationToken);
                return NoContent();
            }

            var rowCount = Math.Min(limit, pkRows.Count);
            var hasMore = pkRows.Count > limit;
            var nextCursor = EncodeCursor(pkRows[rowCount - 1]);

            Response.Headers["X-Row-Count"] = rowCount.ToString();
            Response.Headers["X-Has-More"] = hasMore ? "true" : "false";
            Response.Headers["X-Next-Cursor"] = hasMore ? nextCursor : string.Empty;
            Response.ContentType = "application/x-parquet";

            var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIoFeature != null)
            {
                syncIoFeature.AllowSynchronousIO = true;
            }

            var bytesWritten = 0L;

            try
            {
                await using var connection = new SqlConnection(Context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(
                    $"SELECT TOP (@limit) {selectList} FROM {qualifiedTable} {normalizedWhere} {normalizedOrder}",
                    connection);
                command.CommandTimeout = Options.CommandTimeoutSeconds;
                command.Parameters.AddWithValue("@limit", limit);
                AddQueryParameters(command, parameters);
                AddCursorParameters(command, primaryKeyColumns, cursorValues);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    await RecordAccessLogAsync(accessContext, 0, cancellationToken);
                    await UpdateRateLimitAsync(rateLimitContext, 0, cancellationToken);
                    return NoContent();
                }

                var schema = BuildParquetSchema(reader);
                await using var limitedStream = new NotebookApiLimitedStream(Response.Body, Options.MaxBytesPerResponse);
                await using var parquetWriter = await ParquetWriter.CreateAsync(schema, limitedStream, cancellationToken: cancellationToken);

                var rowGroupSize = Math.Max(1, Options.RowGroupSize);
                var columns = BuildColumnBuffers(reader.FieldCount);
                var rowsWritten = 0;

                do
                {
                    if (rowsWritten >= rowCount)
                    {
                        break;
                    }

                    AppendRow(reader, columns);
                    rowsWritten++;

                    if (rowsWritten % rowGroupSize == 0)
                    {
                        await WriteRowGroupAsync(parquetWriter, schema, columns, cancellationToken);
                    }
                } while (await reader.ReadAsync(cancellationToken));

                if (columns.Any(column => column.Count > 0))
                {
                    await WriteRowGroupAsync(parquetWriter, schema, columns, cancellationToken);
                }

                bytesWritten = limitedStream.BytesWritten;
            }
            catch (NotebookApiResponseTooLargeException)
            {
                if (!Response.HasStarted)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
                    {
                        Title = "Response too large",
                        Detail = "Notebook API response exceeded the configured size limit.",
                        Status = StatusCodes.Status413PayloadTooLarge,
                        Type = "https://httpstatuses.com/413"
                    });
                }

                HttpContext.Abort();
                return new EmptyResult();
            }

            await RecordAccessLogAsync(accessContext, bytesWritten, cancellationToken);
            await UpdateRateLimitAsync(rateLimitContext, bytesWritten, cancellationToken);

            return new EmptyResult();
        }

        protected Task<IActionResult> StreamParquetAsync(
            TableImportTarget target,
            IReadOnlyList<PrimaryKeyColumn> primaryKeyColumns,
            string? whereClause,
            string? orderByClause,
            int limit,
            IReadOnlyList<SqlParameter> parameters,
            object?[]? cursorValues,
            NotebookApiAccessContext? accessContext,
            NotebookApiRateLimitContext? rateLimitContext,
            CancellationToken cancellationToken)
            => StreamParquetAsync(
                target,
                primaryKeyColumns,
                whereClause,
                orderByClause,
                limit,
                parameters,
                cursorValues,
                null,
                accessContext,
                rateLimitContext,
                cancellationToken);

        protected Task<IActionResult> StreamParquetAsync(
            TableImportTarget target,
            IReadOnlyList<PrimaryKeyColumn> primaryKeyColumns,
            int limit,
            object?[]? cursorValues,
            CancellationToken cancellationToken)
            => StreamParquetAsync(target, primaryKeyColumns, limit, cursorValues, null, null, cancellationToken);

        protected int? TryGetNotebookTokenId()
        {
            var claim = User.FindFirst("NotebookTokenId")?.Value;
            return int.TryParse(claim, out var id) ? id : (int?)null;
        }

        private async Task RecordAccessLogAsync(NotebookApiAccessContext? accessContext, long bytesReturned, CancellationToken cancellationToken)
        {
            if (accessContext == null)
            {
                return;
            }

            var logEntry = new NotebookApiAccessLog
            {
                IdMetadonnee = accessContext.DatasetId,
                IdUtilisateur = accessContext.UserId,
                IdNotebookApiToken = accessContext.NotebookTokenId,
                AccessedAtUtc = DateTime.UtcNow,
                BytesReturned = bytesReturned
            };

            Context.NotebookApiAccessLogs.Add(logEntry);
            await Context.SaveChangesAsync(cancellationToken);
        }

        private async Task UpdateRateLimitAsync(NotebookApiRateLimitContext? rateLimitContext, long bytesReturned, CancellationToken cancellationToken)
        {
            if (rateLimitContext == null)
            {
                return;
            }

            var normalizedKey = rateLimitContext.Key.ToLowerInvariant();
            var now = DateTime.UtcNow;
            var entry = await Context.RateLimitEntries.FirstOrDefaultAsync(r => r.Key == normalizedKey, cancellationToken);
            if (entry == null || entry.WindowEndUtc < now)
            {
                var newEntry = entry ?? new RateLimitEntry { Key = normalizedKey };
                newEntry.Count = bytesReturned;
                newEntry.WindowEndUtc = rateLimitContext.WindowEndUtc;
                if (entry == null)
                {
                    Context.RateLimitEntries.Add(newEntry);
                }
                else
                {
                    Context.RateLimitEntries.Update(newEntry);
                }

                await Context.SaveChangesAsync(cancellationToken);
                return;
            }

            entry.Count += bytesReturned;
            entry.WindowEndUtc = rateLimitContext.WindowEndUtc;
            Context.RateLimitEntries.Update(entry);
            await Context.SaveChangesAsync(cancellationToken);
        }

        private static List<List<object?>> BuildColumnBuffers(int fieldCount)
        {
            var columns = new List<List<object?>>(fieldCount);
            for (var i = 0; i < fieldCount; i++)
            {
                columns.Add(new List<object?>());
            }

            return columns;
        }

        private static void AppendRow(SqlDataReader reader, IReadOnlyList<List<object?>> columns)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                columns[i].Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
            }
        }

        private static async Task WriteRowGroupAsync(
            ParquetWriter writer,
            Parquet.Schema.ParquetSchema schema,
            List<List<object?>> columns,
            CancellationToken cancellationToken)
        {
            using var rowGroupWriter = writer.CreateRowGroup();
            for (var i = 0; i < columns.Count; i++)
            {
                var field = (DataField)schema.Fields[i];
                var data = BuildTypedArray(field, columns[i]);
                await rowGroupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(field, data), cancellationToken);
                columns[i].Clear();
            }
        }

        private static Array BuildTypedArray(DataField field, IReadOnlyList<object?> values)
        {
            var baseType = field.ClrType;
            var useNullable = field.IsNullable && baseType.IsValueType;
            var arrayElementType = useNullable ? typeof(Nullable<>).MakeGenericType(baseType) : baseType;
            var typedArray = Array.CreateInstance(arrayElementType, values.Count);

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value == null)
                {
                    typedArray.SetValue(null, i);
                    continue;
                }

                object? convertedValue = value;
                if (!baseType.IsInstanceOfType(value))
                {
                    convertedValue = Convert.ChangeType(value, baseType, CultureInfo.InvariantCulture);
                }

                if (useNullable)
                {
                    convertedValue = Activator.CreateInstance(arrayElementType, convertedValue);
                }

                typedArray.SetValue(convertedValue, i);
            }

            return typedArray;
        }

        private static void AddCursorParameters(SqlCommand command, IReadOnlyList<PrimaryKeyColumn> primaryKeyColumns, object?[]? cursorValues)
        {
            if (cursorValues == null)
            {
                return;
            }

            for (var i = 0; i < primaryKeyColumns.Count; i++)
            {
                var value = cursorValues[i] ?? DBNull.Value;
                var parameter = new SqlParameter($"@pk{i}", primaryKeyColumns[i].DbType)
                {
                    Value = value
                };
                command.Parameters.Add(parameter);
            }
        }

        private static string CombineWhereClauses(string? leftClause, string? rightClause)
        {
            var left = string.IsNullOrWhiteSpace(leftClause) ? null : leftClause.Trim();
            var right = string.IsNullOrWhiteSpace(rightClause) ? null : rightClause.Trim();

            if (left == null)
            {
                return right ?? string.Empty;
            }

            if (right == null)
            {
                return left;
            }

            return $"({left}) AND ({right})";
        }

        private static string BuildSelectList(IReadOnlyList<string>? selectColumns)
        {
            if (selectColumns == null || selectColumns.Count == 0)
            {
                return "*";
            }

            return string.Join(", ", selectColumns
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .Distinct()
                .Select(column => EscapeColumn(column)));
        }

        private static void AddQueryParameters(SqlCommand command, IReadOnlyList<SqlParameter> parameters)
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(CloneParameter(parameter));
            }
        }

        private static SqlParameter CloneParameter(SqlParameter parameter)
        {
            var clone = new SqlParameter(parameter.ParameterName, parameter.SqlDbType)
            {
                Value = parameter.Value,
                Size = parameter.Size,
                Precision = parameter.Precision,
                Scale = parameter.Scale,
                Direction = parameter.Direction
            };

            return clone;
        }
    }
}
