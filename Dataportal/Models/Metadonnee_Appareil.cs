using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class Metadonnee_Appareil
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey("Metadonnee")]
        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

        [Required]
        [ForeignKey("Appareil")]
        public int IdAppareil { get; set; }

        public Appareil Appareil { get; set; }

        public string IdAppareilDansDonnees { get; set; }

        [MaxLength(500)]
        public string Commentaire { get; set; }

    }
}
