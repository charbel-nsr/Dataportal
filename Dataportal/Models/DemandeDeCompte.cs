using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class DemandeDeCompte
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nom { get; set; }

        [Required]
        [StringLength(100)]
        public string Prenom { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string MotDePasseHash { get; set; }

        [Required]
        [ForeignKey("Entreprise")]
        public int IdEntreprise { get; set; }

        public Entreprise Entreprise { get; set; }

        [Required]
        [ForeignKey("StatutDeLaDemande")]
        public int IdStatutDeLaDemande { get; set; }

        public StatutDeLaDemande StatutDeLaDemande { get; set; }

        public bool EmailVerifie { get; set; }

        [Required]
        [StringLength(1000)]
        public string Commentaire { get; set; }

        public DateTime DateCreation { get; set; }

    }
}
