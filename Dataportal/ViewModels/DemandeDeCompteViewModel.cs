using Dataportal.Models;
using System;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class DemandeDeCompteViewModel
    {
        // For displaying the list
        public List<DemandeDeCompte> Demandes { get; set; }

        // For the dropdown filters
        public List<Entreprise> Entreprises { get; set; }
        public List<StatutDeLaDemande> Statuts { get; set; }

        // For filters
        public string Search { get; set; }
        public int? SelectedEntrepriseId { get; set; }
        public int? SelectedStatutId { get; set; }
        public DateTime? DateMin { get; set; }
        public DateTime? DateMax { get; set; }

        //For approving a request
        public List<Role> Roles { get; set; }

    }
}
