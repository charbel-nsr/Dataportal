using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class DomaineEmail
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Domaine { get; set; }

        [Required]
        [ForeignKey("Entreprise")]
        public int IdEntreprise { get; set; }
        public Entreprise Entreprise { get; set; }

        public bool DomaineActif { get; set; }
    }
}
