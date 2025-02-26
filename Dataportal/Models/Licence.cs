using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class Licence
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Libelle { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public ICollection<Metadonnee> Metadonnees { get; set; }

    }
}
