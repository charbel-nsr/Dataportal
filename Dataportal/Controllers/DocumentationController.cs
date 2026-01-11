using Microsoft.AspNetCore.Mvc;

namespace Dataportal.Controllers
{
    public class DocumentationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}