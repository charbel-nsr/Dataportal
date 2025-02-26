using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class Visibilite
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Libelle { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public ICollection<Metadonnee> Metadonnees { get; set; }

    }
}
