using System.ComponentModel.DataAnnotations;

namespace Dataportal.ViewModels
{
    public class ExistingLabelOption
    {
        [Required]
        public string Value { get; set; } = string.Empty;

        [Required]
        public string DisplayText { get; set; } = string.Empty;

        public string Libelle { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }
}