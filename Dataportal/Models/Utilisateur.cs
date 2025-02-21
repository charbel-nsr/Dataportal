namespace Dataportal.Models
{
    public class Utilisateur
    {
        public int Id { get; set; }

        public string Nom { get; set; }

        public string Prenom { get; set; }

        public string Email { get; set; }

        public string MotDePass { get; set; }

        public bool CompteActif { get; set; }

    }
}
