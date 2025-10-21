using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.ViewModels
{
    public class DemanderUnCompteViewModel
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
        [EmailAddress(ErrorMessage = "Email must be a valid address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string MotDePasse { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("MotDePasse", ErrorMessage = "Passwords do not match.")]
        public string? ConfirmMotDePasse { get; set; }

        [Required(ErrorMessage = "Organization is required.")]
        [Display(Name = "Organization")]
        public int IdEntreprise { get; set; }

        public IEnumerable<SelectListItem>? Entreprises { get; set; }

        [StringLength(1000)]
        [Display(Name = "Comment")]
        public string? Commentaire { get; set; }
    }
}
