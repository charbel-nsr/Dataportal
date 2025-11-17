using Dataportal.Models;

namespace Dataportal.ViewModels
{
    public class MetadonneeDetailsViewModel
    {
        // 1️ Core Metadonnee
        public Metadonnee Metadonnee { get; set; }

        // Optionally include related names if not using EF includes
        public Licence Licence { get; set; }
        public Site Site { get; set; }
        public Visibilite Visibilite { get; set; }
        public Utilisateur Utilisateur { get; set; }
        public TypeEnergieRenouvelable? TypeEnergieRenouvelable { get; set; }


        public List<Metadonnee_Appareil> AppareilsLies { get; set; }

        // 2️ Donnees
        public Donnees Donnees { get; set; }
        public List<Dictionary<string, object>> DonneesPreviewRows { get; set; }

        // 3️ EventLogs
        public DonneesEventLogs DonneesEventLogs { get; set; }
        public List<Dictionary<string, object>> EventLogsPreviewRows { get; set; }

        // 4️ ContexteEnvironnemental
        public DonneesContexteEnvironnemental DonneesContexteEnvironnemental { get; set; }
        public List<Dictionary<string, object>> ContextePreviewRows { get; set; }

        // Navigation helpers
        public string? ReturnUrl { get; set; }
    }
}
