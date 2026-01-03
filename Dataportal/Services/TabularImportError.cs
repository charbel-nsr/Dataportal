namespace Dataportal.Services
{
    public class TabularImportError
    {
        public TabularImportError(string fileName, long rowNumber, string columnName, string rawValue, string message)
        {
            FileName = fileName;
            RowNumber = rowNumber;
            ColumnName = columnName;
            RawValue = rawValue;
            Message = message;
        }

        public string FileName { get; }

        public long RowNumber { get; }

        public string ColumnName { get; }

        public string RawValue { get; }

        public string Message { get; }
    }
}