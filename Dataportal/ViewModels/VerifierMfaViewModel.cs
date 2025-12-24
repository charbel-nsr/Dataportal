using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels;

public class VerifierMfaViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    [Display(Name = "Verification code")]
    public string Code { get; set; }

    public bool SeSouvenirDeMoi { get; set; }

    public string? ReturnUrl { get; set; }
}