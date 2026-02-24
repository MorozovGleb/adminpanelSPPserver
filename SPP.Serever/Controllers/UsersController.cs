using Microsoft.AspNetCore.Mvc;

namespace SPP.Serever.Controllers
{
    public class UsersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
