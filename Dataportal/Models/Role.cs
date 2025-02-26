using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class Role
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Libelle { get; set; }

        [MaxLength(200)]
        public string Description { get; set; }

        public ICollection<Utilisateur> Utilisateurs { get; set; }

    }
}
