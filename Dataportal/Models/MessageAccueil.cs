using System;
using System.ComponentModel.DataAnnotations;

namespace Dataportal.Models
{
    public class MessageAccueil
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Contenu { get; set; } = string.Empty;

        public bool VisibleAuxInvites { get; set; }

        public DateTime DateDerniereModification { get; set; } = DateTime.UtcNow;
    }
}