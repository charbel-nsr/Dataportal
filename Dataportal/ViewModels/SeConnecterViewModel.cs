using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels
{
    public class SeConnecterViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email must be a valid address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string MotDePasse { get; set; }

        [Display(Name = "Remember me")]
        public bool SeSouvenirDeMoi { get; set; }
    }
}
