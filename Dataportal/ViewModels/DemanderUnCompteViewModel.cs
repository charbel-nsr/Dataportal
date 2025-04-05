using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.ViewModels
{
    public class DemanderUnCompteViewModel
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

        [Required(ErrorMessage = "Le mot de passe est requis.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe")]
        public string MotDePasse { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("MotDePasse", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string? ConfirmMotDePasse { get; set; }

        [Required(ErrorMessage = "L'Établissement est requis.")]
        [Display(Name = "Établissement")]
        public int IdEntreprise { get; set; }

        public IEnumerable<SelectListItem>? Entreprises { get; set; }

        [StringLength(1000)]
        [Display(Name = "Commentaire")]
        public string? Commentaire { get; set; }
    }
}
