using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels
{
    public class NotebookTokensViewModel
    {
        [Required(ErrorMessage = "A label is required.")]
        [StringLength(100)]
        [Display(Name = "Token label")]
        public string? Label { get; set; }

        public string? NewToken { get; set; }

        public List<NotebookTokenListItemViewModel> Tokens { get; set; } = new();
    }

    public class NotebookTokenListItemViewModel
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? LastUsedAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }
    }
}