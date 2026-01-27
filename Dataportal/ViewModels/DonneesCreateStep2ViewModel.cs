using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Dataportal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Dataportal.ViewModels
{
    public class DonneesCreateStep2ViewModel : IValidatableObject
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Label")]
        public string Libelle { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Number of sensors")]
        public int NombreDeCapteurs { get; set; }

        [Display(Name = "Collection frequency")]
        public string FrequenceDeCollect { get; set; }

        [Required]
        [Display(Name = "Start timestamp")]
        [DataType(DataType.DateTime)]
        public DateTime StartTimestamp { get; set; }

        [Required]
        [Display(Name = "End timestamp")]
        [DataType(DataType.DateTime)]
        public DateTime EndTimestamp { get; set; }

        [Display(Name = "Files (CSV, XLSX, Parquet, or CSV.zip)")]
        public List<IFormFile>? UploadedFiles { get; set; }

        [Required(ErrorMessage = "Data quality is required.")]
        [Display(Name = "Data quality")]
        public int? IdQualiteDonnees { get; set; }

        public IEnumerable<SelectListItem> QualiteOptions { get; set; } = new List<SelectListItem>();

        public IDictionary<string, string> QualiteDescriptions { get; set; } = new Dictionary<string, string>();

        [Display(Name = "Base on existing data")]
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
            if (string.IsNullOrWhiteSpace(UploadSessionId) && (UploadedFiles == null || !UploadedFiles.Any()))
            {
                yield return new ValidationResult(
                    "You must upload at least one data file (CSV, XLSX, Parquet, or CSV.zip).",
                    new[] { nameof(UploadedFiles) });
            }

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

    public class ColumnTypeSelectionViewModel
    {
        [Required]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        public string SelectedType { get; set; } = TabularColumnType.NVarChar.ToString();

        public int? MaxLength { get; set; }

        public string? InferredType { get; set; }

        public int? InferredLength { get; set; }
    }

    public class PersistedFileSummary
    {
        public string Name { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }
}