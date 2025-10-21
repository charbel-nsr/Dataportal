using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels
{
    public class MetadonneeAppareilInfo
    {
        [Required]
        public int IdAppareil { get; set; }

        [Display(Name = "Device ID in the data")]
        public string? IdAppareilDansDonnees { get; set; }

        [Display(Name = "Comment")]
        public string? Commentaire { get; set; }
    }
}