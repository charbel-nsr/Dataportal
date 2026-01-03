using System;

namespace Dataportal.Services
{
    public class TabularColumnDefinition
    {
        public TabularColumnDefinition(string name, TabularColumnType columnType, int? maxLength = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Column name is required.", nameof(name));
            }

            Name = name.Trim();
            ColumnType = columnType;
            MaxLength = maxLength;
        }

        public string Name { get; }

        public TabularColumnType ColumnType { get; }

        public int? MaxLength { get; }

        public TabularColumnDefinition WithType(TabularColumnType type, int? maxLength = null)
        {
            return new TabularColumnDefinition(Name, type, maxLength ?? MaxLength);
        }
    }
}