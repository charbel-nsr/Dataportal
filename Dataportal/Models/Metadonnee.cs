using NuGet.Packaging.Signing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Eventing.Reader;

namespace Dataportal.Models
{
    public class Metadonnee
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }

        [Required]
        [ForeignKey("Licence")]
        public int IdLicence { get; set; }

        public Licence Licence { get; set; }

        [MaxLength(10)]
        public string TailleDesDonnees { get; set; }

        public int NombreDeTelechargements { get; set; }

        public DateTime? DernierMiseAJour { get; set; }

        [Required]
        [ForeignKey("Documentation")]
        public int IdDocumentation { get; set; }

        public Documentation Documentation { get; set; }

        public bool SeriesTemporelles { get; set; }

        public int QualiteDesDonnees { get; set; }

        [ForeignKey("Site")]
        public int IdSite { get; set; }

        public Site Site { get; set; }

        [Required]
        [ForeignKey("Visibilite")]
        public int IdVisibilite { get; set; }

        public Visibilite Visibilite { get; set; }

        [Required]
        [ForeignKey("utilisateur")]
        public int IdUtilisateur { get; set; }

        public Utilisateur Utilisateur { get; set; }

        public DateTime StartTimestamp { get; set; }

        public DateTime EndTimestamp { get; set; }

        public bool AutoriserApi { get; set; }

        public bool Anonymiser { get; set; }

        public bool AutoriserLeTelechargement { get; set; }

        public bool VisualiserLesdonnees { get; set; }

        public bool AutoriserLesSQL { get; set; }

        public ICollection<Documentation> Documentations { get; set; }

        public ICollection<Metadonnee_Appareil> Metadonnee_Appareils { get; set; }

        public ICollection<Schema_Metadonnee> schema_Metadonnees { get; set; }

        public ICollection<Historique> Historiques { get; set; }

        [ForeignKey("Donnees")]
        public int IdDonnees { get; set; }

        public Donnees Donnees { get; set; }

        [ForeignKey("DonneesEventLogs")]
        public int IdDonneesEventLogs { get; set; }

        public DonneesEventLogs DonneesEventLogs { get; set; }

        [ForeignKey("DonneesContexteEnvironnemental")]
        public int IdDonneesContexteEnvironnemental { get; set; }

        public DonneesContexteEnvironnemental DonneesContexteEnvironnemental { get; set; }
        
    }
}
