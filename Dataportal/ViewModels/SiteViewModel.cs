using Dataportal.Models;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class SiteViewModel
    {
        public List<Site> Sites { get; set; }
        public string Search { get; set; }
        public bool? Actif { get; set; }
    }
}
