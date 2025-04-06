using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dataportal.ViewModels
{
    public class MotDePasseOublieViewModel
    {
        [Required(ErrorMessage = "L'Email est requis.")]
        [EmailAddress(ErrorMessage = "L'email doit être une adresse valide.")]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}
