using System.ComponentModel.DataAnnotations;
using Dataportal.Models;
using Microsoft.AspNetCore.Http;

namespace Dataportal.ViewModels
{
    public class FichierStockeSearchViewModel
    {
        public string? Search { get; set; }
        public double? MinFileSizeMb { get; set; }
        public double? MaxFileSizeMb { get; set; }
        public int? IdCreateur { get; set; }
        public int? IdVisibilite { get; set; }
        public int? IdLicence { get; set; }
        public int? IdEntreprise { get; set; }
        public bool? AutoriserLeTelechargement { get; set; }

        public IEnumerable<LookupItem>? Createurs { get; set; }
        public IEnumerable<LookupItem>? Visibilites { get; set; }
        public IEnumerable<LookupItem>? Entreprises { get; set; }
        public IEnumerable<Licence>? Licences { get; set; }

        public List<FichierStocke> Fichiers { get; set; } = new();
    }

    public class FichierStockeUploadViewModel
    {
        [Required]
        [MaxLength(150)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public int IdVisibilite { get; set; }

        [Required]
        public int IdLicence { get; set; }

        public int? IdTypeEnergieRenouvelable { get; set; }

        public bool AutoriserLeTelechargement { get; set; }

        [Required]
        public IFormFile? UploadedFile { get; set; }

        public IEnumerable<Visibilite>? Visibilites { get; set; }
        public IEnumerable<Licence>? Licences { get; set; }
        public IEnumerable<TypeEnergieRenouvelable>? TypesEnergieRenouvelable { get; set; }
    }

    public class FichierStockeEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nom { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public int IdVisibilite { get; set; }

        [Required]
        public int IdLicence { get; set; }

        public int? IdTypeEnergieRenouvelable { get; set; }

        public bool AutoriserLeTelechargement { get; set; }

        public IEnumerable<Visibilite>? Visibilites { get; set; }
        public IEnumerable<Licence>? Licences { get; set; }
        public IEnumerable<TypeEnergieRenouvelable>? TypesEnergieRenouvelable { get; set; }
    }
}