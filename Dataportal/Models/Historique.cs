using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class Historique
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey("Metadonnee")]
        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

        public DateTime Date { get; set; }

        public string Lien { get; set; }

        public string Description { get; set; }
    }
}
