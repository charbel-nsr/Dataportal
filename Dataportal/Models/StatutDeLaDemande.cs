using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class StatutDeLaDemande
    {
        public StatutDeLaDemande()
        {
            DemandeDeComptes = new List<DemandeDeCompte>();
        }

        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Libelle { get; set; }

        [MaxLength(200)]
        public string Description { get; set; }

        public ICollection<DemandeDeCompte> DemandeDeComptes { get; set; }

    }
}
