using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeelShare.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
       
        public IActionResult Index() => RedirectToAction("Index", "Quotes", new { area = "Admin" });
    }
}