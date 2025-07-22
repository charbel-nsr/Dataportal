using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels
{
    public class MetadonneeAppareilInfo
    {
        [Required]
        public int IdAppareil { get; set; }

        [Display(Name = "Id de l'appareil dans les données")]
        public string? IdAppareilDansDonnees { get; set; }

        [Display(Name = "Commentaire")]
        public string? Commentaire { get; set; }
    }
}