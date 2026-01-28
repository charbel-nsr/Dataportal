using Dataportal.Context;
using Dataportal.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Services
{
    public class NotebookReplaceSessionService
    {
        private readonly ApplicationDbContext _context;

        public NotebookReplaceSessionService(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<int> AbortExpiredSessionsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        {
            var sessions = await _context.NotebookReplaceSessions
                .Include(s => s.Metadonnee)
                .Where(s => s.CreatedAtUtc <= cutoffUtc
                    && s.Status != NotebookReplaceStatus.Aborted
                    && s.Status != NotebookReplaceStatus.Pushed)
                .ToListAsync(cancellationToken);

            var abortedCount = 0;
            foreach (var session in sessions)
            {
                if (await AbortSessionAsync(session, cancellationToken))
                {
                    abortedCount++;
                }
            }

            return abortedCount;
        }

        public async Task<bool> AbortSessionAsync(NotebookReplaceSession session, CancellationToken cancellationToken)
        {
            if (session.Status == NotebookReplaceStatus.Aborted || session.Status == NotebookReplaceStatus.Pushed)
            {
                return false;
            }

            var sourceTarget = new TableImportTarget(session.Schema, session.TableName);
            var stagingTarget = new TableImportTarget(session.Schema, session.StagingTableName);
            var oldTarget = string.IsNullOrWhiteSpace(session.OldTableName)
                ? null
                : new TableImportTarget(session.Schema, session.OldTableName);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var connection = (SqlConnection)_context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var sqlTransaction = _context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            if (session.Status == NotebookReplaceStatus.Committed && oldTarget != null)
            {
                if (await TableExistsAsync(oldTarget, connection, sqlTransaction, cancellationToken))
                {
                    if (await TableExistsAsync(sourceTarget, connection, sqlTransaction, cancellationToken))
                    {
                        await DropTableAsync(sourceTarget, connection, sqlTransaction, cancellationToken);
                    }

                    await RenameTableAsync(oldTarget, session.TableName, connection, sqlTransaction, cancellationToken);
                }
            }
            else if (await TableExistsAsync(stagingTarget, connection, sqlTransaction, cancellationToken))
            {
                await DropTableAsync(stagingTarget, connection, sqlTransaction, cancellationToken);
            }

            await UnlockMetadonneeAsync(session.IdMetadonnee, cancellationToken);

            session.Status = NotebookReplaceStatus.Aborted;
            session.CompletedAtUtc = DateTime.UtcNow;
            session.UpdatedAtUtc = session.CompletedAtUtc;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }

        private async Task UnlockMetadonneeAsync(int metadonneeId, CancellationToken cancellationToken)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Metadonnee SET TraitementEnCours = 0 WHERE Id = {metadonneeId}",
                cancellationToken);
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
    }
}