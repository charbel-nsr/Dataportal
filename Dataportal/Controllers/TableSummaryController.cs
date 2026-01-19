using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Dataportal.Context;
using Dataportal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dataportal.Controllers
{
    [ApiController]
    [Route("api/table-summary")]
    public class TableSummaryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] KnownSchemas =
        {
            TableImportSchemas.Donnees,
            TableImportSchemas.DonneesEventLogs,
            TableImportSchemas.DonneesContexteEnvironnemental
        };

        public TableSummaryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get([FromQuery] string tableName, [FromQuery] string fallbackSchema)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return BadRequest("Table name is required.");
            }

            if (!TryBuildTableImportTarget(tableName, fallbackSchema, out var target))
            {
                return BadRequest("Invalid table name.");
            }

            var summaries = new List<ColumnSummary>();

            using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var columns = await GetColumnDefinitions(connection, target);

            foreach (var column in columns)
            {
                var summary = await GetColumnSummary(connection, target, column);
                summaries.Add(summary);
            }

            return Ok(new { columns = summaries });
        }

        private static async Task<List<ColumnDefinition>> GetColumnDefinitions(SqlConnection connection, TableImportTarget target)
        {
            var columns = new List<ColumnDefinition>();
            const string query = @"
                SELECT COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                ORDER BY ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@schema", target.Schema);
            command.Parameters.AddWithValue("@table", target.TableName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnDefinition(
                    reader.GetString(0),
                    reader.GetString(1)));
            }

            return columns;
        }

        private static async Task<ColumnSummary> GetColumnSummary(SqlConnection connection, TableImportTarget target, ColumnDefinition column)
        {
            var safeColumn = column.Name.Replace("]", "]]", StringComparison.Ordinal);
            var columnExpression = $"[{safeColumn}]";
            var typeCategory = GetTypeCategory(column.DataType);
            string query;

            switch (typeCategory)
            {
                case ColumnTypeCategory.Number:
                    query = $@"
                        SELECT
                            COUNT(*) AS TotalCount,
                            COUNT(DISTINCT {columnExpression}) AS UniqueCount,
                            MIN({columnExpression}) AS MinValue,
                            MAX({columnExpression}) AS MaxValue,
                            AVG(CAST({columnExpression} AS float)) AS AvgValue
                        FROM {target.QualifiedNameWithDatabase}";
                    break;
                case ColumnTypeCategory.Date:
                    query = $@"
                        SELECT
                            COUNT(*) AS TotalCount,
                            COUNT(DISTINCT {columnExpression}) AS UniqueCount,
                            MIN({columnExpression}) AS MinValue,
                            MAX({columnExpression}) AS MaxValue
                        FROM {target.QualifiedNameWithDatabase}";
                    break;
                case ColumnTypeCategory.Text:
                    query = $@"
                        SELECT
                            COUNT(*) AS TotalCount,
                            COUNT(DISTINCT {columnExpression}) AS UniqueCount,
                            MIN(LEN({columnExpression})) AS MinLength,
                            MAX(LEN({columnExpression})) AS MaxLength,
                            AVG(CAST(LEN({columnExpression}) AS float)) AS AvgLength
                        FROM {target.QualifiedNameWithDatabase}";
                    break;
                default:
                    query = $@"
                        SELECT
                            COUNT(*) AS TotalCount,
                            COUNT(DISTINCT {columnExpression}) AS UniqueCount
                        FROM {target.QualifiedNameWithDatabase}";
                    break;
            }

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new ColumnSummary(column.Name, typeCategory.ToString(), null, null, null, null, null, null, null);
            }

            var uniqueCount = ReadLong(reader, "UniqueCount");

            return new ColumnSummary(
                column.Name,
                typeCategory.ToString(),
                uniqueCount,
                ReadString(reader, "MinValue"),
                ReadString(reader, "MaxValue"),
                ReadString(reader, "AvgValue"),
                ReadString(reader, "MinLength"),
                ReadString(reader, "MaxLength"),
                ReadString(reader, "AvgLength"));
        }

        private static long? ReadLong(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        private static string? ReadString(SqlDataReader reader, string name)
        {
            if (!reader.HasColumn(name))
            {
                return null;
            }

            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        private static bool TryBuildTableImportTarget(string? storedName, string? fallbackSchema, out TableImportTarget? target)
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

            if (string.IsNullOrWhiteSpace(fallbackSchema))
            {
                return false;
            }

            target = new TableImportTarget(fallbackSchema.Trim(), storedName.Trim());
            return true;
        }

        private static bool IsKnownSchema(string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return false;
            }

            foreach (var knownSchema in KnownSchemas)
            {
                if (schema.Equals(knownSchema, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ColumnTypeCategory GetTypeCategory(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
            {
                return ColumnTypeCategory.Other;
            }

            var normalized = dataType.Trim().ToLowerInvariant();

            return normalized switch
            {
                "bit" => ColumnTypeCategory.Boolean,
                "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney"
                    => ColumnTypeCategory.Number,
                "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or "time"
                    => ColumnTypeCategory.Date,
                "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext"
                    => ColumnTypeCategory.Text,
                _ => ColumnTypeCategory.Other
            };
        }

        private record ColumnDefinition(string Name, string DataType);

        private record ColumnSummary(
            string Name,
            string DataType,
            long? UniqueCount,
            string? MinValue,
            string? MaxValue,
            string? AvgValue,
            string? MinLength,
            string? MaxLength,
            string? AvgLength);

        private enum ColumnTypeCategory
        {
            Boolean,
            Number,
            Date,
            Text,
            Other
        }
    }

    internal static class SqlDataReaderExtensions
    {
        public static bool HasColumn(this SqlDataReader reader, string name)
        {
            for (var i = 0; i < reader.FieldCount; i += 1)
            {
                if (reader.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}