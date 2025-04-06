using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dataportal.Models
{
    public class Utilisateur
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
        [ForeignKey("Role")]
        public int IdRole { get; set; }

        public Role Role { get; set; }

        public bool CompteActif { get; set; }

        public DateTime DateApprobation { get; set; }

        public DateTime? DateModification { get; set; }

        public DateTime? DernierLogin { get; set; }

        public ICollection<Metadonnee> Metadonnees { get; set; }

        public int NbrEchecsAcces { get; set; }

        public DateTime? FinLockout { get; set; }

        [StringLength(300)]
        public string LienLinkedIn { get; set; }

        [StringLength(1000)]
        public string DescriptionProfil { get; set; }
    }
}
