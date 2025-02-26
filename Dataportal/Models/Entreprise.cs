using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class Entreprise
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; }

        public bool Actif { get; set; }

        public virtual ICollection<Utilisateur> Utilisateurs { get; set; }

        public virtual ICollection<DemandeDeCompte> DemandeDeComptes { get; set; }

        public ICollection<DomaineEmail> DomaineEmails { get; set; }

    }
}
