using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class QualiteDonnees
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Libelle { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public ICollection<Donnees> Donnees { get; set; }

        public ICollection<DonneesContexteEnvironnemental> DonneesContexteEnvironnemental { get; set; }

        public ICollection<DonneesEventLogs> DonneesEventLogs { get; set; }

    }
}