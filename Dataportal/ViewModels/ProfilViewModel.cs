using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.ViewModels
{
    public class ProfilViewModel
    {

        [Required(ErrorMessage = "Your last name is required.")]
        [StringLength(100)]
        [Display(Name = "Last name")]
        public string Nom { get; set; }

        [Required(ErrorMessage = "Your first name is required.")]
        [StringLength(100)]
        [Display(Name = "First name")]
        public string Prenom { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Organization")]
        public string Entreprise { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        [StringLength(300)]
        [Display(Name = "LinkedIn link")]
        public string? LienLinkedIn { get; set; }

        [StringLength(1000)]
        [Display(Name = "Biography")]
        public string? DescriptionProfil { get; set; }
    }
}
