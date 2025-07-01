using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class Appareil
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }

        public string Capacite { get; set; }

        public string Model { get; set; }

        public string Manufacturer { get; set; }

        public bool Actif { get; set; }

        public ICollection<Metadonnee_Appareil> Metadonnee_Appareils { get; set; }

    }
}
