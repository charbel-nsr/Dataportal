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

        public DateTime StartTimestamp { get; set; }

        public DateTime EndTimestamp { get; set; }

        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

        public int IdQualiteDonnees { get; set; }

        public QualiteDonnees QualiteDonnees { get; set; }
    }
}
