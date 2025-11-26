using System;

namespace Dataportal.Services
{
    public class TableImportTarget
    {
        public TableImportTarget(string schema, string tableName)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                throw new ArgumentException("Schema name is required.", nameof(schema));
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name is required.", nameof(tableName));
            }

            Schema = schema.Trim();
            TableName = tableName.Trim();
        }

        public string Schema { get; }

        public string TableName { get; }

        public string QualifiedNameWithDatabase => $"[DataPortal].[{GetSafeSchema()}].[{GetSafeTableName()}]";

        public string SchemaQualifiedName => $"[{GetSafeSchema()}].[{GetSafeTableName()}]";

        public string ObjectIdLiteral => QualifiedNameWithDatabase.Replace("'", "''", StringComparison.Ordinal);

        public string GetSafeSchema() => Schema.Replace("]", "]]", StringComparison.Ordinal);

        public string GetSafeTableName() => TableName.Replace("]", "]]", StringComparison.Ordinal);
    }
}