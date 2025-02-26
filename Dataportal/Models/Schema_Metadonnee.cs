using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class Schema_Metadonnee
    {
        public int Id { get; set; }

        public string Libelle { get; set; }

        [Required]
        [ForeignKey("Schema")]
        public int IdSchema { get; set; }

        public Schema Schema { get; set; }

        [Required]
        [ForeignKey("Metadonnee")]
        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

        public String Description { get; set; }

        public Array Arguments { get; set; }
    }
}
