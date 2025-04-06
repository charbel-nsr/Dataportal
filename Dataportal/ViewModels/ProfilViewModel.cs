using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.ViewModels
{
    public class ProfilViewModel
    {

        [Required(ErrorMessage = "Votre Nom est requis.")]
        [StringLength(100)]
        [Display(Name = "Nom")]
        public string Nom { get; set; }

        [Required(ErrorMessage = "Votre Prénom est requis.")]
        [StringLength(100)]
        [Display(Name = "Prénom")]
        public string Prenom { get; set; }

        [Required(ErrorMessage = "L'Email est requis.")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Établissement")]
        public string Entreprise { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        [StringLength(300)]
        [Display(Name = "Lien LinkedIn")]
        public string? LienLinkedIn { get; set; }

        [StringLength(1000)]
        [Display(Name = "Biographie")]
        public string? DescriptionProfil { get; set; }
    }
}
