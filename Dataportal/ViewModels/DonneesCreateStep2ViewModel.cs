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
        [Display(Name = "Libellé")]
        public string Libelle { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Nombre de capteurs")]
        public int NombreDeCapteurs { get; set; }

        [Display(Name = "Fréquence de collecte")]
        public string FrequenceDeCollect { get; set; }

        [Required]
        [Display(Name = "Timestamp de début")]
        [DataType(DataType.DateTime)]
        public DateTime StartTimestamp { get; set; }

        [Required]
        [Display(Name = "Timestamp de fin")]
        [DataType(DataType.DateTime)]
        public DateTime EndTimestamp { get; set; }

        [Required(ErrorMessage = "Veuillez importer au moins un fichier de données (CSV, XLSX, Parquet ou CSV.zip).")]
        [Display(Name = "Fichiers (CSV, XLSX, Parquet ou CSV.zip)")]
        public List<IFormFile> UploadedFiles { get; set; }

        [Required(ErrorMessage = "La qualité des données est requise.")]
        [Display(Name = "Qualité des données")]
        public int? IdQualiteDonnees { get; set; }

        public IEnumerable<SelectListItem> QualiteOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTimestamp > EndTimestamp)
            {
                yield return new ValidationResult(
                    "Le timestamp de début doit précéder le timestamp de fin.",
                    new[] { nameof(StartTimestamp), nameof(EndTimestamp) });
            }
        }
    }
}
