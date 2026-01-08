using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Dataportal.Services;

namespace Dataportal.ViewModels
{
    public class DonneesContexteEditStep4ViewModel : IValidatableObject
    {
        [Required]
        public int IdMetadonnee { get; set; }

        [Required(ErrorMessage = "Label is required.")]
        [MaxLength(50)]
        [Display(Name = "Label")]
        public string Libelle { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Code is required.")]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [Display(Name = "Start date")]
        [DataType(DataType.DateTime)]
        public DateTime StartTimestamp { get; set; }

        [Display(Name = "End date")]
        [DataType(DataType.DateTime)]
        public DateTime EndTimestamp { get; set; }

        [Display(Name = "Files (CSV, XLSX, Parquet, or CSV.zip)")]
        public ICollection<IFormFile>? UploadedFiles { get; set; }

        [Required(ErrorMessage = "Data quality is required.")]
        [Display(Name = "Data quality")]
        public int? IdQualiteDonnees { get; set; }

        public IEnumerable<SelectListItem> QualiteOptions { get; set; } = new List<SelectListItem>();

        public IDictionary<string, string> QualiteDescriptions { get; set; } = new Dictionary<string, string>();

        public bool CanUploadFiles { get; set; }

        public string? UploadSessionId { get; set; }

        public bool ColumnTypesConfirmed { get; set; }

        public bool ProceedAfterImportErrors { get; set; }

        public List<ColumnTypeSelectionViewModel> ColumnTypes { get; set; } = new();

        public List<TabularImportError> ImportErrors { get; set; } = new();

        public List<PersistedFileSummary> PersistedFiles { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTimestamp > EndTimestamp)
            {
                yield return new ValidationResult(
                    "The start date must be before the end date.",
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
        }
    }
}
