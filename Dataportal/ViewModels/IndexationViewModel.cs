using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class IndexationViewModel
    {
        public string? Search { get; set; }

        public string? Status { get; set; }

        public string? Type { get; set; }

        public List<IndexationJobItemViewModel> Jobs { get; set; } = new();
    }

    public class IndexationJobItemViewModel
    {
        public int RecordId { get; set; }

        public string RecordType { get; set; } = string.Empty;

        public int MetadonneeId { get; set; }

        public string DatasetName { get; set; } = string.Empty;

        public string TableName { get; set; } = string.Empty;

        public string IndexName { get; set; } = string.Empty;

        public string IndexStatus { get; set; } = string.Empty;

        public string? IndexTimeColumn { get; set; }

        public string? IndexIdColumn { get; set; }

        public string? IndexIncludeColumn { get; set; }

        public string? IndexError { get; set; }
    }
}