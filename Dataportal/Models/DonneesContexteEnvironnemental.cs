using NuGet.Packaging.Signing;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class DonneesContexteEnvironnemental
    {
        public int Id { get; set; }

        public string Libelle { get; set; }

        public string Code { get; set; }

        public string NomDeLaTable { get; set; }

        public string Description { get; set; }

        public DateTime DateAjouter { get; set; }

        public Timestamp Timestamp { get; set; }

        [ForeignKey("Metadonnee")]
        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }
    }
}
