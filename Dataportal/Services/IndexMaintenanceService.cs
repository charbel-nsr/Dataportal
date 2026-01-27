using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dataportal.Context;
using Dataportal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dataportal.Services
{
    public class IndexMaintenanceService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<IndexMaintenanceService> _logger;

        public IndexMaintenanceService(ApplicationDbContext dbContext, ILogger<IndexMaintenanceService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task RunPendingIndexesAsync(CancellationToken stoppingToken)
        {
            return ProcessPendingIndexesAsync(_dbContext, stoppingToken);
        }

        public async Task<IndexRunResult> RunIndexNowAsync(IndexationTarget target, CancellationToken stoppingToken)
        {
            return target.Type switch
            {
                "donnees" => await RunIndexNowAsync(
                    _dbContext.Donnees,
                    TableImportSchemas.Donnees,
                    target.RecordId,
                    entity => entity.IndexEnabled,
                    entity => entity.NomDeLaTable,
                    entity => entity.IndexName,
                    entity => entity.IndexTimeColumn,
                    entity => entity.IndexIdColumn,
                    entity => entity.IndexIncludeColumn,
                    entity => entity.IndexStatus,
                    (entity, status) => entity.IndexStatus = status,
                    (entity, error) => entity.IndexError = error,
                    stoppingToken),
                "eventlogs" => await RunIndexNowAsync(
                    _dbContext.DonneesEventLogs,
                    TableImportSchemas.DonneesEventLogs,
                    target.RecordId,
                    entity => entity.IndexEnabled,
                    entity => entity.NomDeLaTable,
                    entity => entity.IndexName,
                    entity => entity.IndexTimeColumn,
                    entity => entity.IndexIdColumn,
                    entity => entity.IndexIncludeColumn,
                    entity => entity.IndexStatus,
                    (entity, status) => entity.IndexStatus = status,
                    (entity, error) => entity.IndexError = error,
                    stoppingToken),
                "contexte" => await RunIndexNowAsync(
                    _dbContext.DonneesContexteEnvironnemental,
                    TableImportSchemas.DonneesContexteEnvironnemental,
                    target.RecordId,
                    entity => entity.IndexEnabled,
                    entity => entity.NomDeLaTable,
                    entity => entity.IndexName,
                    entity => entity.IndexTimeColumn,
                    entity => entity.IndexIdColumn,
                    entity => entity.IndexIncludeColumn,
                    entity => entity.IndexStatus,
                    (entity, status) => entity.IndexStatus = status,
                    (entity, error) => entity.IndexError = error,
                    stoppingToken),
                _ => IndexRunResult.Failed("Unknown target type.")
            };
        }

        private async Task ProcessPendingIndexesAsync(ApplicationDbContext dbContext, CancellationToken stoppingToken)
        {
            await ProcessPendingIndexesAsync(
                dbContext,
                dbContext.Donnees,
                TableImportSchemas.Donnees,
                entity => entity.IndexEnabled,
                entity => entity.NomDeLaTable,
                entity => entity.IndexName,
                entity => entity.IndexTimeColumn,
                entity => entity.IndexIdColumn,
                entity => entity.IndexIncludeColumn,
                entity => entity.IndexStatus,
                (entity, status) => entity.IndexStatus = status,
                (entity, error) => entity.IndexError = error,
                stoppingToken);

            await ProcessPendingIndexesAsync(
                dbContext,
                dbContext.DonneesEventLogs,
                TableImportSchemas.DonneesEventLogs,
                entity => entity.IndexEnabled,
                entity => entity.NomDeLaTable,
                entity => entity.IndexName,
                entity => entity.IndexTimeColumn,
                entity => entity.IndexIdColumn,
                entity => entity.IndexIncludeColumn,
                entity => entity.IndexStatus,
                (entity, status) => entity.IndexStatus = status,
                (entity, error) => entity.IndexError = error,
                stoppingToken);

            await ProcessPendingIndexesAsync(
                dbContext,
                dbContext.DonneesContexteEnvironnemental,
                TableImportSchemas.DonneesContexteEnvironnemental,
                entity => entity.IndexEnabled,
                entity => entity.NomDeLaTable,
                entity => entity.IndexName,
                entity => entity.IndexTimeColumn,
                entity => entity.IndexIdColumn,
                entity => entity.IndexIncludeColumn,
                entity => entity.IndexStatus,
                (entity, status) => entity.IndexStatus = status,
                (entity, error) => entity.IndexError = error,
                stoppingToken);
        }

        private async Task ProcessPendingIndexesAsync<TEntity>(
            ApplicationDbContext dbContext,
            DbSet<TEntity> dbSet,
            string fallbackSchema,
            Func<TEntity, bool> enabledSelector,
            Func<TEntity, string?> tableSelector,
            Func<TEntity, string?> indexNameSelector,
            Func<TEntity, string?> timeColumnSelector,
            Func<TEntity, string?> idColumnSelector,
            Func<TEntity, string?> includeColumnSelector,
            Func<TEntity, string?> statusSelector,
            Action<TEntity, string?> statusSetter,
            Action<TEntity, string?> errorSetter,
            CancellationToken stoppingToken)
            where TEntity : class
        {
            var pendingItems = await dbSet
                .Where(entity => enabledSelector(entity) && statusSelector(entity) == "pending")
                .ToListAsync(stoppingToken);

            foreach (var entity in pendingItems)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                statusSetter(entity, "running");
                errorSetter(entity, null);
                await dbContext.SaveChangesAsync(stoppingToken);

                var result = await TryCreateIndexAsync(
                    dbContext,
                    fallbackSchema,
                    tableSelector(entity),
                    indexNameSelector(entity),
                    timeColumnSelector(entity),
                    idColumnSelector(entity),
                    includeColumnSelector(entity),
                    stoppingToken);

                statusSetter(entity, result.Status);
                errorSetter(entity, result.Error);
                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }

        private async Task<IndexRunResult> RunIndexNowAsync<TEntity>(
            DbSet<TEntity> dbSet,
            string fallbackSchema,
            int recordId,
            Func<TEntity, bool> enabledSelector,
            Func<TEntity, string?> tableSelector,
            Func<TEntity, string?> indexNameSelector,
            Func<TEntity, string?> timeColumnSelector,
            Func<TEntity, string?> idColumnSelector,
            Func<TEntity, string?> includeColumnSelector,
            Func<TEntity, string?> statusSelector,
            Action<TEntity, string?> statusSetter,
            Action<TEntity, string?> errorSetter,
            CancellationToken stoppingToken)
            where TEntity : class
        {
            var entity = await dbSet.FindAsync([recordId], stoppingToken);
            if (entity == null)
            {
                return IndexRunResult.Failed("Index job not found.");
            }

            if (!enabledSelector(entity))
            {
                return IndexRunResult.Failed("Indexing is not enabled for this dataset.");
            }

            var currentStatus = statusSelector(entity);
            if (string.Equals(currentStatus, "running", StringComparison.OrdinalIgnoreCase))
            {
                return IndexRunResult.Failed("Indexing is already running.");
            }

            statusSetter(entity, "running");
            errorSetter(entity, null);
            await _dbContext.SaveChangesAsync(stoppingToken);

            var result = await TryCreateIndexAsync(
                _dbContext,
                fallbackSchema,
                tableSelector(entity),
                indexNameSelector(entity),
                timeColumnSelector(entity),
                idColumnSelector(entity),
                includeColumnSelector(entity),
                stoppingToken);

            statusSetter(entity, result.Status);
            errorSetter(entity, result.Error);
            await _dbContext.SaveChangesAsync(stoppingToken);

            return result.Status == "completed"
                ? IndexRunResult.Completed()
                : IndexRunResult.Failed(result.Error ?? "Indexing failed.");
        }

        private async Task<IndexCreationResult> TryCreateIndexAsync(
            ApplicationDbContext dbContext,
            string fallbackSchema,
            string? storedTableName,
            string? indexName,
            string? timeColumn,
            string? idColumn,
            string? includeColumn,
            CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(storedTableName))
            {
                return IndexCreationResult.Failed("Table name is missing.");
            }

            if (string.IsNullOrWhiteSpace(timeColumn))
            {
                return IndexCreationResult.Failed("Time column is missing.");
            }

            var (schemaName, tableName) = ParseSchemaAndTable(storedTableName, fallbackSchema);

            var normalizedIndexName = string.IsNullOrWhiteSpace(indexName)
                ? BuildDefaultIndexName(tableName, idColumn)
                : indexName;

            var safeIndexName = QuoteIdentifier(normalizedIndexName);
            var safeSchema = QuoteIdentifier(schemaName);
            var safeTable = QuoteIdentifier(tableName);
            var safeTimeColumn = QuoteIdentifier(timeColumn);
            var safeIdColumn = string.IsNullOrWhiteSpace(idColumn) ? null : QuoteIdentifier(idColumn);
            var includeClause = string.IsNullOrWhiteSpace(includeColumn)
                ? string.Empty
                : $" INCLUDE ({QuoteIdentifier(includeColumn)})";
            var columnClause = safeIdColumn == null
                ? safeTimeColumn
                : $"{safeIdColumn}, {safeTimeColumn}";

            var fullyQualifiedNameLiteral = EscapeSqlLiteral($"{schemaName}.{tableName}");
            var indexNameLiteral = EscapeSqlLiteral(normalizedIndexName);

            var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{indexNameLiteral}' AND object_id = OBJECT_ID(N'{fullyQualifiedNameLiteral}'))
BEGIN
    CREATE INDEX {safeIndexName} ON {safeSchema}.{safeTable} ({columnClause}){includeClause}
END";

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(sql, stoppingToken);
                return IndexCreationResult.Completed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create index {IndexName} on {Schema}.{Table}.", normalizedIndexName, schemaName, tableName);
                return IndexCreationResult.Failed(TruncateError(ex.Message));
            }
        }

        private static (string Schema, string Table) ParseSchemaAndTable(string storedName, string fallbackSchema)
        {
            var parts = storedName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                return (parts[0].Trim(), parts[1].Trim());
            }

            return (fallbackSchema, storedName.Trim());
        }

        private static string BuildDefaultIndexName(string tableName, string? idColumn)
        {
            var suffix = string.IsNullOrWhiteSpace(idColumn) ? "time" : "id_time";
            return $"IX_{tableName}_{suffix}";
        }

        private static string QuoteIdentifier(string identifier)
        {
            var sanitized = identifier.Replace("]", "]]", StringComparison.Ordinal);
            return $"[{sanitized}]";
        }

        private static string EscapeSqlLiteral(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal);
        }

        private static string TruncateError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Unknown error.";
            }

            return message.Length <= 1000 ? message : message.Substring(0, 1000);
        }

        private readonly record struct IndexCreationResult(string Status, string? Error)
        {
            public static IndexCreationResult Completed() => new("completed", null);

            public static IndexCreationResult Failed(string error) => new("failed", error);
        }
    }

    public readonly record struct IndexRunResult(bool Success, string Message)
    {
        public static IndexRunResult Completed() => new(true, "Indexation started.");

        public static IndexRunResult Failed(string message) => new(false, message);
    }

    public readonly record struct IndexationTarget(string Type, int RecordId)
    {
        public static IndexationTarget Donnees(int recordId) => new("donnees", recordId);

        public static IndexationTarget EventLogs(int recordId) => new("eventlogs", recordId);

        public static IndexationTarget Contexte(int recordId) => new("contexte", recordId);

        public static IndexationTarget From(string? type, int recordId)
        {
            return type?.ToLowerInvariant() switch
            {
                "donnees" => Donnees(recordId),
                "eventlogs" => EventLogs(recordId),
                "contexte" => Contexte(recordId),
                _ => new IndexationTarget(string.Empty, recordId)
            };
        }
    }
}