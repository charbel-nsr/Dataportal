using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class FichierStocke
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [ForeignKey("Licence")]
        public int IdLicence { get; set; }

        public Licence? Licence { get; set; }

        [ForeignKey("TypeEnergieRenouvelable")]
        public int? IdTypeEnergieRenouvelable { get; set; }

        public TypeEnergieRenouvelable? TypeEnergieRenouvelable { get; set; }

        public bool AutoriserLeTelechargement { get; set; }

        [Required]
        [MaxLength(255)]
        public string NomFichierOriginal { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string NomFichierStocke { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TypeContenu { get; set; } = "application/octet-stream";

        public long TailleOctets { get; set; }

        [Required]
        [MaxLength(64)]
        public string HashSha256 { get; set; } = string.Empty;

        public DateTime DateAjout { get; set; }

        public int NombreDeTelechargements { get; set; }

        [Required]
        [ForeignKey("Visibilite")]
        public int IdVisibilite { get; set; }

        public Visibilite? Visibilite { get; set; }

        [Required]
        [ForeignKey("Utilisateur")]
        public int IdUtilisateur { get; set; }

        public Utilisateur? Utilisateur { get; set; }
    }
}