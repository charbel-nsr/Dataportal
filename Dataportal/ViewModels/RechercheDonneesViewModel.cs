using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class RechercheDonneesViewModel
    {
        // Filters
        public string? Search { get; set; }
        public int? IdLicence { get; set; }
        public int? IdTypeEnergieRenouvelable { get; set; }
        public bool? SeriesTemporelles { get; set; }
        public bool? Anonymiser { get; set; }
        public bool? AutoriserLeTelechargement { get; set; }
        public bool? HasEventLogs { get; set; }
        public bool? HasContextEnv { get; set; }

        // Dropdown data
        public IEnumerable<Licence>? Licences { get; set; }
        public IEnumerable<TypeEnergieRenouvelable>? TypesEnergieRenouvelable { get; set; }

        // Result
        public List<Metadonnee> Metadonnees { get; set; } = new();
    }
}
