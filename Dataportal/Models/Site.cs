using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class Site
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Non { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public string Emplacement { get; set; }

        public ICollection<Metadonnee> Metadonnees { get; set; }
    }
}
