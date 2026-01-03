using System.Collections.Generic;

namespace Dataportal.Services
{
    public class TabularImportResult
    {
        public int RowsCopied { get; set; }

        public List<TabularImportError> Errors { get; set; } = new();

        public bool TableCreated { get; set; }
    }
}