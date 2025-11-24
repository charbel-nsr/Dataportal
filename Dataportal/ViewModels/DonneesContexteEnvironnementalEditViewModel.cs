using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Dataportal.ViewModels
{
    public class DonneesContexteEnvironnementalEditViewModel : IValidatableObject
    {
        [Required]
        public int IdMetadonnee { get; set; }

        [Required]
        public int IdDonneesContexteEnvironnemental { get; set; }

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

        [Required(ErrorMessage = "Data quality is required.")]
        [Display(Name = "Data quality")]
        public int? IdQualiteDonnees { get; set; }

        public IEnumerable<SelectListItem> QualiteOptions { get; set; } = new List<SelectListItem>();

        public IDictionary<string, string> QualiteDescriptions { get; set; } = new Dictionary<string, string>();

        public string ReturnUrl { get; set; } = string.Empty;

        public string BannerMessage { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTimestamp > EndTimestamp)
            {
                yield return new ValidationResult(
                    "The start date must be before the end date.",
                    new[] { nameof(StartTimestamp), nameof(EndTimestamp) });
            }
        }
    }
}
