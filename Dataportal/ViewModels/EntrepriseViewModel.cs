using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class EntrepriseViewModel
    {
        public IEnumerable<Entreprise> Entreprises { get; set; }

        // Filters
        public string Search { get; set; }
        public bool? Actif { get; set; }
    }
}
