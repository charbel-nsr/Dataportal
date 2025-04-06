using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.ViewModels
{
    public class ChangerMotDePasseViewModel
    {

        [Required(ErrorMessage = "L'Email est requis.")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Le mot de passe actuel est requis.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe actuel")]
        public string MotDePasseActuel { get; set; }

        [Required(ErrorMessage = "Le nouveau mot de passe est requis.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nouveau mot de passe")]
        public string NouveauMotDePasse { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("NouveauMotDePasse", ErrorMessage = "Le nouveau mot de passe ne correspond pas.")]
        public string? ConfirmNouveauMotDePasse { get; set; }
    }
}
