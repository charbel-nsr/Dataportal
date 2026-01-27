using NuGet.Packaging.Signing;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class DonneesEventLogs
    {
        public int Id { get; set; }

        public string Libelle { get; set; }

        public string Code { get; set; }

        public string NomDeLaTable { get; set; }

        public string Description { get; set; }

        public DateTime DateAjouter { get; set; }

        public DateTime StartTimestamp { get; set; }

        public DateTime EndTimestamp { get; set; }

        public int NombreDEvents { get; set; }

        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

        public int IdQualiteDonnees { get; set; }

        public QualiteDonnees QualiteDonnees { get; set; }

        public bool IndexEnabled { get; set; }

        public string? IndexTimeColumn { get; set; }

        public string? IndexIdColumn { get; set; }

        public string? IndexIncludeColumn { get; set; }

        public string? IndexType { get; set; }

        public string? IndexName { get; set; }

        public string? IndexStatus { get; set; }

        public string? IndexError { get; set; }
    }
}
