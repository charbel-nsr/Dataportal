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
            var distinctExpression = GetDistinctExpression(column, columnExpression);
            var typeCategory = GetTypeCategory(column.DataType);
            switch (typeCategory)
            {
                case ColumnTypeCategory.Number:
                    var numberUniqueCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT(DISTINCT {distinctExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var numberNonNullCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var numberMinValue = await ExecuteScalarString(connection, $@"
                        SELECT MIN({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var numberMaxValue = await ExecuteScalarString(connection, $@"
                        SELECT MAX({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var numberAvgValue = await ExecuteScalarString(connection, $@"
                        SELECT AVG(CAST({columnExpression} AS float))
                        FROM {target.QualifiedNameWithDatabase}");
                    var numberStdDevValue = await ExecuteScalarString(connection, $@"
                        SELECT STDEV(CAST({columnExpression} AS float))
                        FROM {target.QualifiedNameWithDatabase}");

                    return new ColumnSummary(
                        column.Name,
                        column.DataType,
                        typeCategory.ToString(),
                        numberUniqueCount,
                        numberNonNullCount,
                        numberMinValue,
                        numberMaxValue,
                        numberAvgValue,
                        numberStdDevValue,
                        null,
                        null,
                        null);
                case ColumnTypeCategory.Date:
                    var dateUniqueCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT(DISTINCT {distinctExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var dateNonNullCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var dateMinValue = await ExecuteScalarString(connection, $@"
                        SELECT MIN({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var dateMaxValue = await ExecuteScalarString(connection, $@"
                        SELECT MAX({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");

                    return new ColumnSummary(
                        column.Name,
                        column.DataType,
                        typeCategory.ToString(),
                        dateUniqueCount,
                        dateNonNullCount,
                        dateMinValue,
                        dateMaxValue,
                        null,
                        null,
                        null,
                        null,
                        null);
                case ColumnTypeCategory.Text:
                    var textUniqueCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT(DISTINCT {distinctExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var textNonNullCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var textMinLength = await ExecuteScalarString(connection, $@"
                        SELECT MIN(LEN({columnExpression}))
                        FROM {target.QualifiedNameWithDatabase}");
                    var textMaxLength = await ExecuteScalarString(connection, $@"
                        SELECT MAX(LEN({columnExpression}))
                        FROM {target.QualifiedNameWithDatabase}");
                    var textAvgLength = await ExecuteScalarString(connection, $@"
                        SELECT AVG(CAST(LEN({columnExpression}) AS float))
                        FROM {target.QualifiedNameWithDatabase}");

                    return new ColumnSummary(
                        column.Name,
                        column.DataType,
                        typeCategory.ToString(),
                        textUniqueCount,
                        textNonNullCount,
                        null,
                        null,
                        null,
                        null,
                        textMinLength,
                        textMaxLength,
                        textAvgLength);
                default:
                    var otherUniqueCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT(DISTINCT {distinctExpression})
                        FROM {target.QualifiedNameWithDatabase}");
                    var otherNonNullCount = await ExecuteScalarLong(connection, $@"
                        SELECT COUNT({columnExpression})
                        FROM {target.QualifiedNameWithDatabase}");

                    return new ColumnSummary(
                        column.Name,
                        column.DataType,
                        typeCategory.ToString(),
                        otherUniqueCount,
                        otherNonNullCount,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);
            }

            return new ColumnSummary(column.Name, column.DataType, typeCategory.ToString(), null, null, null, null, null, null, null, null, null);
        }

        private static async Task<long?> ExecuteScalarLong(SqlConnection connection, string query)
        {
            using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            if (result is long longValue)
            {
                return longValue;
            }

            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        private static async Task<string?> ExecuteScalarString(SqlConnection connection, string query)
        {
            using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            if (result is DateTime dateTime)
            {
                return dateTime.ToString("O", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(result, CultureInfo.InvariantCulture);
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

        private static string GetDistinctExpression(ColumnDefinition column, string columnExpression)
        {
            var normalized = column.DataType?.Trim().ToLowerInvariant();

            return normalized switch
            {
                "text" or "ntext" or "image" => $"CAST({columnExpression} AS nvarchar(max))",
                _ => columnExpression
            };
        }

        private record ColumnDefinition(string Name, string DataType);

        private record ColumnSummary(
            string Name,
            string DataType,
            string Category,
            long? UniqueCount,
            long? NonNullCount,
            string? MinValue,
            string? MaxValue,
            string? AvgValue,
            string? StdDevValue,
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
}