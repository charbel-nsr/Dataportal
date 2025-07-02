using Dataportal.Models;
using System.Collections.Generic;

namespace Dataportal.ViewModels
{
    public class AppareilViewModel
    {
        public List<Appareil> Appareils { get; set; }
        public string Search { get; set; }
        public bool? Actif { get; set; }
    }
}