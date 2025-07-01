using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class UtilisateursAdminViewModel
    {
        public List<Utilisateur> Utilisateurs { get; set; }

        public List<Entreprise> Entreprises { get; set; }
        public List<Role> Roles { get; set; }

        // Filters
        public string Search { get; set; }
        public int? SelectedEntrepriseId { get; set; }
        public int? SelectedRoleId { get; set; }
        public bool? CompteActif { get; set; }
    }

}
