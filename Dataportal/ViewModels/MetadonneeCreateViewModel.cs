using System.ComponentModel.DataAnnotations;
using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class MetadonneeCreateViewModel
    {
        [Required(ErrorMessage = "Le nom est requis.")]
        [MaxLength(100)]
        [Display(Name = "Nom")]
        public string Nom { get; set; }

        [Required(ErrorMessage = "La description est requise.")]
        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "La licence est requise.")]
        [Display(Name = "Licence")]
        public int IdLicence { get; set; }

        [Required(ErrorMessage = "Le site est requis.")]
        [Display(Name = "Site")]
        public int IdSite { get; set; }

        [Required(ErrorMessage = "La visibilité est requise.")]
        [Display(Name = "Visibilité")]
        public int IdVisibilite { get; set; }

        [Display(Name = "Séries temporelles")]
        public bool SeriesTemporelles { get; set; }

        [Display(Name = "Autoriser l'accès API")]
        public bool AutoriserApi { get; set; }

        [Display(Name = "Anonymiser les données")]
        public bool Anonymiser { get; set; }

        [Display(Name = "Autoriser le téléchargement")]
        public bool AutoriserLeTelechargement { get; set; }

        public IEnumerable<Site>? Sites { get; set; }
        public IEnumerable<Licence>? Licences { get; set; }
        public IEnumerable<Visibilite>? Visibilites { get; set; }

        [Required(ErrorMessage = "Les Appareils sont requis.")]
        [Display(Name = "Appareils")]
        public IEnumerable<int> SelectedAppareils { get; set; }
        public IEnumerable<Appareil>? Appareils { get; set; }

        public List<MetadonneeAppareilInfo> AppareilInfos { get; set; } = new();
    }
}
