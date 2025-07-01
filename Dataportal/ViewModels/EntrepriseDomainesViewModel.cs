namespace Dataportal.ViewModels
{
    public class DomaineEmailViewModel
    {
        public int Id { get; set; }
        public string Domaine { get; set; }
        public bool DomaineActif { get; set; }
    }

    public class EntrepriseDomainesViewModel
    {
        public int EntrepriseId { get; set; }
        public string EntrepriseNom { get; set; }
        public List<DomaineEmailViewModel> Domaines { get; set; }
        public string NewDomaine { get; set; }
    }
}
