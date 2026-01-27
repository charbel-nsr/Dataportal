using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Dataportal.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Dataportal.ViewModels
{
    public class DonneesEventLogsCreateStep3ViewModel : IValidatableObject
    {
        [Required]
        public int IdMetadonnee { get; set; }

        [Required(ErrorMessage = "Label is required.")]
        [MaxLength(100)]
        [Display(Name = "Label")]
        public string Libelle { get; set; }

        [Required(ErrorMessage = "Code is required.")]
        [MaxLength(100)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Start timestamp is required.")]
        [Display(Name = "Start timestamp")]
        public DateTime StartTimestamp { get; set; }

        [Required(ErrorMessage = "End timestamp is required.")]
        [Display(Name = "End timestamp")]
        public DateTime EndTimestamp { get; set; }

        [Display(Name = "Number of events")]
        public int NombreDEvents { get; set; }

        [Display(Name = "Files (CSV, XLSX, Parquet, or CSV.zip)")]
        public ICollection<IFormFile>? UploadedFiles { get; set; }

        [Required(ErrorMessage = "Data quality is required.")]
        [Display(Name = "Data quality")]
        public int? IdQualiteDonnees { get; set; }

        public IEnumerable<SelectListItem> QualiteOptions { get; set; } = new List<SelectListItem>();

        public IDictionary<string, string> QualiteDescriptions { get; set; } = new Dictionary<string, string>();

        [Display(Name = "Base on existing event logs")]
        public int? IdExistingMetadonnee { get; set; }

        public List<ExistingLabelOption> ExistingLabelOptions { get; set; } = new();

        public string? UploadSessionId { get; set; }

        public bool ColumnTypesConfirmed { get; set; }

        public bool ProceedAfterImportErrors { get; set; }

        public List<ColumnTypeSelectionViewModel> ColumnTypes { get; set; } = new();

        public List<TabularImportError> ImportErrors { get; set; } = new();

        public List<PersistedFileSummary> PersistedFiles { get; set; } = new();

        public bool IsTimeSeries { get; set; }

        [Display(Name = "Index the table after import")]
        public bool IndexEnabled { get; set; }

        [Display(Name = "Time column")]
        public string? IndexTimeColumn { get; set; }

        [Display(Name = "Sensor/Group ID column")]
        public string? IndexIdColumn { get; set; }

        [Display(Name = "Include column")]
        public string? IndexIncludeColumn { get; set; }

        public IEnumerable<SelectListItem> IndexTimeColumnOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> IndexIdColumnOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> IndexIncludeColumnOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTimestamp > EndTimestamp)
            {
                yield return new ValidationResult(
                    "The start timestamp must be before the end timestamp.",
                    new[] { nameof(StartTimestamp), nameof(EndTimestamp) });
            }

            if (ColumnTypesConfirmed)
            {
                if (ColumnTypes == null || ColumnTypes.Count == 0)
                {
                    yield return new ValidationResult(
                        "Column types must be confirmed before continuing.",
                        new[] { nameof(ColumnTypes) });
                }
                else
                {
                    foreach (var column in ColumnTypes)
                    {
                        if (!Enum.TryParse<TabularColumnType>(column.SelectedType, out _))
                        {
                            yield return new ValidationResult(
                                $"Invalid column type provided for {column.ColumnName}.",
                                new[] { nameof(ColumnTypes) });
                            break;
                        }

                        if (string.Equals(column.SelectedType, TabularColumnType.NVarChar.ToString(), StringComparison.OrdinalIgnoreCase)
                            && column.MaxLength.HasValue && column.MaxLength.Value <= 0)
                        {
                            yield return new ValidationResult(
                                $"Please provide a length greater than zero for column {column.ColumnName}.",
                                new[] { nameof(ColumnTypes) });
                            break;
                        }
                    }
                }
            }

            if (IsTimeSeries && IndexEnabled && string.IsNullOrWhiteSpace(IndexTimeColumn))
            {
                yield return new ValidationResult(
                    "Please select a time column for indexing.",
                    new[] { nameof(IndexTimeColumn) });
            }
        }
    }
}
