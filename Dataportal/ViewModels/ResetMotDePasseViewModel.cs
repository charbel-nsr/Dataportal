using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels;

public class ResetMotDePasseViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NouveauMotDePasse { get; set; }

    [Required]
    [Compare("NouveauMotDePasse", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmationMotDePasse { get; set; }
}