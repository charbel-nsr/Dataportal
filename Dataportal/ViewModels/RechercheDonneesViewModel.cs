using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class LookupItem
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;
    }

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
        public bool? AutoriserApi { get; set; }
        public double? MinDataSizeMb { get; set; }
        public double? MaxDataSizeMb { get; set; }
        public int? IdCreateur { get; set; }
        public int? IdVisibilite { get; set; }
        public int? IdEntreprise { get; set; }

        // Dropdown data
        public IEnumerable<Licence>? Licences { get; set; }
        public IEnumerable<TypeEnergieRenouvelable>? TypesEnergieRenouvelable { get; set; }
        public IEnumerable<LookupItem>? Createurs { get; set; }
        public IEnumerable<LookupItem>? Visibilites { get; set; }
        public IEnumerable<LookupItem>? Entreprises { get; set; }

        // Result
        public List<Metadonnee> Metadonnees { get; set; } = new();
    }
}
