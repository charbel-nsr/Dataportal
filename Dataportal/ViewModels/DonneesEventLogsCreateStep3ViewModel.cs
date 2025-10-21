using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
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

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTimestamp > EndTimestamp)
            {
                yield return new ValidationResult(
                    "The start timestamp must be before the end timestamp.",
                    new[] { nameof(StartTimestamp), nameof(EndTimestamp) });
            }
        }
    }
}
