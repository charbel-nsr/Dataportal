using System;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class NotebookApiAccessLogsViewModel
    {
        public int? DatasetId { get; set; }
        public int? UserId { get; set; }
        public int? TokenId { get; set; }
        public DateTime? DateFromUtc { get; set; }
        public DateTime? DateToUtc { get; set; }
        public List<NotebookApiAccessLogItemViewModel> Logs { get; set; } = new();
    }

    public class NotebookApiAccessLogItemViewModel
    {
        public int Id { get; set; }
        public int DatasetId { get; set; }
        public string DatasetName { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? UserDisplayName { get; set; }
        public int? TokenId { get; set; }
        public string? TokenLabel { get; set; }
        public DateTime AccessedAtUtc { get; set; }
        public long BytesReturned { get; set; }
    }
}