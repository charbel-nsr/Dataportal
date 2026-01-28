using Dataportal.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class NotebookReplaceSessionsViewModel
    {
        public int? DatasetId { get; set; }

        public int? UserId { get; set; }

        public NotebookReplaceStatus? Status { get; set; }

        public DateTime? CreatedFromUtc { get; set; }

        public DateTime? CreatedToUtc { get; set; }

        public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();

        public IReadOnlyList<NotebookReplaceSessionItemViewModel> Sessions { get; set; } = Array.Empty<NotebookReplaceSessionItemViewModel>();

        public bool IsAdminView { get; set; }
    }

    public class NotebookReplaceSessionItemViewModel
    {
        public Guid Id { get; set; }

        public int DatasetId { get; set; }

        public string DatasetName { get; set; } = string.Empty;

        public string Schema { get; set; } = string.Empty;

        public string TableName { get; set; } = string.Empty;

        public string StagingTableName { get; set; } = string.Empty;

        public string? OldTableName { get; set; }

        public NotebookReplaceStatus Status { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int? UserId { get; set; }

        public string? UserDisplayName { get; set; }
    }
}