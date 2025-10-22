using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class TypeEnergieRenouvelable
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Libelle { get; set; }

        public ICollection<Metadonnee>? Metadonnees { get; set; }
    }
}