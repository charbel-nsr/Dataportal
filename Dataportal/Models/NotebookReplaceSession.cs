using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class NotebookReplaceSession
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public int IdMetadonnee { get; set; }

        public Metadonnee Metadonnee { get; set; }

        [Required]
        [MaxLength(128)]
        public string Schema { get; set; }

        [Required]
        [MaxLength(128)]
        public string TableName { get; set; }

        [Required]
        [MaxLength(128)]
        public string StagingTableName { get; set; }

        [MaxLength(128)]
        public string? OldTableName { get; set; }

        [ForeignKey("Utilisateur")]
        public int? IdUtilisateur { get; set; }

        public Utilisateur? Utilisateur { get; set; }

        [Required]
        public NotebookReplaceStatus Status { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

        public DateTime? CommittedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
    }
}