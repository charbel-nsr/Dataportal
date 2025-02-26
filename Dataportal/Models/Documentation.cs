using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class Documentation
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Libelle { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public string Lien { get; set; }

        [Required]
        [ForeignKey("Metadonnee")]
        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

    }
}
