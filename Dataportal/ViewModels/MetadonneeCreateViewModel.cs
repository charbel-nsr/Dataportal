using System.ComponentModel.DataAnnotations;
using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class MetadonneeCreateViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(100)]
        [Display(Name = "Name")]
        public string Nom { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "License is required.")]
        [Display(Name = "License")]
        public int IdLicence { get; set; }

        [Required(ErrorMessage = "Site is required.")]
        [Display(Name = "Site")]
        public int IdSite { get; set; }

        [Required(ErrorMessage = "Visibility is required.")]
        [Display(Name = "Visibility")]
        public int IdVisibilite { get; set; }

        [Display(Name = "Time series")]
        public bool SeriesTemporelles { get; set; }

        [Display(Name = "Allow API access")]
        public bool AutoriserApi { get; set; }

        [Display(Name = "Anonymize data")]
        public bool Anonymiser { get; set; }

        [Display(Name = "Allow downloads")]
        public bool AutoriserLeTelechargement { get; set; }

        public IEnumerable<Site>? Sites { get; set; }
        public IEnumerable<Licence>? Licences { get; set; }
        public IEnumerable<Visibilite>? Visibilites { get; set; }

        [Required(ErrorMessage = "Devices are required.")]
        [Display(Name = "Devices")]
        public IEnumerable<int> SelectedAppareils { get; set; }
        public IEnumerable<Appareil>? Appareils { get; set; }

        public List<MetadonneeAppareilInfo> AppareilInfos { get; set; } = new();
    }
}
