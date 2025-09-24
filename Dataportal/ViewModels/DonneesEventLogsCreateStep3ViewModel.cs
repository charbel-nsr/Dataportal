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

        [Required(ErrorMessage = "Le libellé est requis.")]
        [MaxLength(100)]
        [Display(Name = "Libellé")]
        public string Libelle { get; set; }

        [Required(ErrorMessage = "Le code est requis.")]
        [MaxLength(100)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Le timestamp de début est requis.")]
        [Display(Name = "Timestamp début")]
        public DateTime StartTimestamp { get; set; }

        [Required(ErrorMessage = "Le timestamp de fin est requis.")]
        [Display(Name = "Timestamp fin")]
        public DateTime EndTimestamp { get; set; }

        [Display(Name = "Nombre d'événements")]
        public int NombreDEvents { get; set; }

        [Display(Name = "Fichiers (CSV, XLSX, Parquet ou CSV.zip)")]
        public ICollection<IFormFile>? UploadedFiles { get; set; }

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
