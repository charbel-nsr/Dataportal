using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class DonneesContexteEnvironnementalCreateStep4ViewModel : IValidatableObject
    {
        [Required]
        public int IdMetadonnee { get; set; }

        [Required(ErrorMessage = "Le libellé est requis.")]
        [MaxLength(50)]
        [Display(Name = "Libellé")]
        public string Libelle { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Le code est requis.")]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [Display(Name = "Date de début")]
        [DataType(DataType.DateTime)]
        public DateTime StartTimestamp { get; set; }

        [Display(Name = "Date de fin")]
        [DataType(DataType.DateTime)]
        public DateTime EndTimestamp { get; set; }

        [Display(Name = "Fichiers (CSV, XLSX, Parquet ou CSV.zip)")]
        public List<IFormFile> UploadedFiles { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTimestamp > EndTimestamp)
            {
                yield return new ValidationResult(
                    "La date de début doit précéder la date de fin.",
                    new[] { nameof(StartTimestamp), nameof(EndTimestamp) });
            }
        }
    }
}