using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeelShare.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
       // Главная страница админки: просто перекидываю на Quotes
        // (чтобы /Admin сразу открывал нужный раздел)
        public IActionResult Index() => RedirectToAction("Index", "Quotes", new { area = "Admin" });
    }
}