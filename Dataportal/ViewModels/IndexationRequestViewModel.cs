using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dataportal.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Dataportal.ViewModels
{
    public class IndexationRequestViewModel : IValidatableObject
    {
        public string TabId { get; set; } = string.Empty;

        public string TabTitle { get; set; } = string.Empty;

        [Required]
        public int RecordId { get; set; }

        [Required]
        public string RecordType { get; set; } = string.Empty;

        public int MetadonneeId { get; set; }

        public string DatasetName { get; set; } = string.Empty;

        public string TableName { get; set; } = string.Empty;

        public bool IsTimeSeries { get; set; }

        public bool IsIndexed { get; set; }

        public bool IndexEnabled { get; set; } = true;

        [Display(Name = "Time column")]
        public string? IndexTimeColumn { get; set; }

        [Display(Name = "Sensor/Group ID column")]
        public string? IndexIdColumn { get; set; }

        [Display(Name = "Include column")]
        public string? IndexIncludeColumn { get; set; }

        public IEnumerable<SelectListItem> IndexTimeColumnOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> IndexIdColumnOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> IndexIncludeColumnOptions { get; set; } = new List<SelectListItem>();

        public string? ReturnUrl { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsTimeSeries && IndexEnabled && string.IsNullOrWhiteSpace(IndexTimeColumn))
            {
                yield return new ValidationResult(
                    "Please select a time column for indexing.",
                    new[] { nameof(IndexTimeColumn) });
            }
        }
    }
}