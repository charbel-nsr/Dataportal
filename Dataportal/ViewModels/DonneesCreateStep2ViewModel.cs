using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Dataportal.ViewModels
{
    public class DonneesCreateStep2ViewModel
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

        [Required(ErrorMessage = "Veuillez importer au moins un fichier CSV.")]
        [Display(Name = "Fichiers CSV")]
        public List<IFormFile> UploadedFiles { get; set; }
    }
}
