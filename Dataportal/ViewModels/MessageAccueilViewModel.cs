using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels;

public class MessageAccueilViewModel
{
    [MaxLength(4000)]
    [Display(Name = "Message content")]
    public string Contenu { get; set; } = string.Empty;

    [Display(Name = "Visible to non-logged-in users")]
    public bool VisibleAuxInvites { get; set; }
}