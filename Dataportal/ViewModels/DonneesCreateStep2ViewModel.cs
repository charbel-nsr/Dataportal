using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

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

        [Required(ErrorMessage = "Please upload at least one data file (CSV, XLSX, Parquet, or CSV.zip).")]
        [Display(Name = "Files (CSV, XLSX, Parquet, or CSV.zip)")]
        public List<IFormFile> UploadedFiles { get; set; }

        [Required(ErrorMessage = "Data quality is required.")]
        [Display(Name = "Data quality")]
        public int? IdQualiteDonnees { get; set; }

        public IEnumerable<SelectListItem> QualiteOptions { get; set; } = new List<SelectListItem>();

        public IDictionary<string, string> QualiteDescriptions { get; set; } = new Dictionary<string, string>();

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
