using Dataportal.Models;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class LicenceViewModel
    {
        public List<Licence> Licences { get; set; }
        public string Search { get; set; }
        public bool? Actif { get; set; }
    }
}
