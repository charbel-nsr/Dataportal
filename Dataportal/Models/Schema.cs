namespace Dataportal.Models
{
    public class Schema
    {
        public int Id { get; set; }

        public string Libelle { get; set; }

        public string Description { get; set; }

        //public Array Arguments { get; set; }

        public ICollection<Schema_Metadonnee> schema_Metadonnees { get; set; }

    }
}
