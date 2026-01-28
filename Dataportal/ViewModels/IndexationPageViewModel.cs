using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class IndexationPageViewModel
    {
        public int MetadonneeId { get; set; }

        public string DatasetName { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }

        public string ActiveTabId { get; set; } = string.Empty;

        public List<IndexationRequestViewModel> Requests { get; set; } = new();
    }
}