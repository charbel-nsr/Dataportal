using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class NotebookApiAccessLog
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Metadonnee))]
        public int IdMetadonnee { get; set; }

        public Metadonnee? Metadonnee { get; set; }

        [ForeignKey(nameof(Utilisateur))]
        public int? IdUtilisateur { get; set; }

        public Utilisateur? Utilisateur { get; set; }

        [ForeignKey(nameof(NotebookApiToken))]
        public int? IdNotebookApiToken { get; set; }

        public NotebookApiToken? NotebookApiToken { get; set; }

        public DateTime AccessedAtUtc { get; set; }

        public long BytesReturned { get; set; }
    }
}