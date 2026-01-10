using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.Models
{
    public class NotebookApiToken
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Label { get; set; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? LastUsedAtUtc { get; set; }

        public DateTime? RevokedAtUtc { get; set; }

        [Required]
        [ForeignKey(nameof(Utilisateur))]
        public int IdUtilisateur { get; set; }

        public Utilisateur? Utilisateur { get; set; }
    }
}