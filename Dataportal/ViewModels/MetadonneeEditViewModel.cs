using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class MetadonneeEditViewModel : MetadonneeCreateViewModel
    {
        public int Id { get; set; }

        public string ReturnUrl { get; set; } = string.Empty;

        public new List<MetadonneeAppareilInfo> AppareilInfos { get; set; } = new();
    }
}
