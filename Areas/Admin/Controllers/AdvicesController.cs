using System.Linq;
using System.Threading.Tasks;
using FeelShare.Web.Areas.Admin.ViewModels;
using FeelShare.Web.Data;
using FeelShare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeelShare.Web.Areas.Admin.Controllers
{
    // Явно говорю MVC, что этот контроллер лежит в Area = "Admin"
    [Area("Admin")]

    [Authorize(Roles = "Admin")]
    public class AdvicesController : Controller
    {
        
        private readonly AppDbContext _db;

      
        public AdvicesController(AppDbContext db) => _db = db;

        // Маленькая утилита: определяю AJAX-запрос по заголовку

        private static bool IsAjax(Microsoft.AspNetCore.Http.HttpRequest req) =>
            req.Headers["X-Requested-With"] == "XMLHttpRequest";

        
        // Главная страница админки советов:
      
        public async Task<IActionResult> Index(int? id, string? q)
        {
            // Беру список советов с фильтром по строке поиска (q)
            // AsNoTracking() — быстрее, это просто чтение для вывода
            var list = await Filtered(q)
                .OrderBy(a => a.Category).ThenBy(a => a.Id)
                .AsNoTracking()
                .ToListAsync();

            // Если id пришёл — значит открыли режим редактирования конкретного совета
            // Если не нашли — подстраховка: отдаю пустой объект
            var formModel = id is int editId
                ? await _db.MoodAdvices.FindAsync(editId) ?? new MoodAdvice()
                : new MoodAdvice();

            // Собираю ViewModel: список + форма + текущий поисковый запрос
            return View(new AdvicesIndexVM
            {
                List = list,
                Form = formModel,
                Query = q
            });
        }

        // Partial: список обновления
        // Это отдельный endpoint, чтобы обновлять только кусок страницы (без полной перезагрузки)
        [HttpGet]
        public async Task<IActionResult> List(string? q)
        {
            var list = await Filtered(q)
                .OrderBy(a => a.Category).ThenBy(a => a.Id)
                .AsNoTracking()
                .ToListAsync();

          
            return PartialView("~/Areas/Admin/Views/Advices/_AdviceList.cshtml", list);
        }

        // Partial: форма редактировать
        // Возвращаю только форму 
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            var model = id is int editId
                ? (await _db.MoodAdvices.FindAsync(editId)) ?? new MoodAdvice()
                : new MoodAdvice();

            return PartialView("~/Areas/Admin/Views/Advices/_AdviceForm.cshtml", model);
        }

        // Создать
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(MoodAdvice model, string? q)
        {
          
            model.Category = (model.Category ?? "neutral").Trim().ToLower();

            // Разрешаю только 3 категории
            var allowed = new[] { "bad", "neutral", "good" };
            if (!allowed.Contains(model.Category))
                ModelState.AddModelError(nameof(model.Category),
                    "Категория должна быть: bad / neutral / good.");

          
            if (!ModelState.IsValid)
            {
                var listAll = await _db.MoodAdvices.AsNoTracking()
                    .OrderBy(a => a.Category).ThenBy(a => a.Id)
                    .ToListAsync();

                return View("Index", new AdvicesIndexVM
                {
                    List = listAll,
                    Form = model,
                    Query = q
                });
            }

          
            if (model.Id == 0)
            {
                _db.MoodAdvices.Add(model);
                await _db.SaveChangesAsync();

                
                if (IsAjax(Request))
                    return Json(new { ok = true, message = "Совет добавлен." });
            }
            else
            {
                // Иначе — обновление существующего
                var dbItem = await _db.MoodAdvices.FindAsync(model.Id);
                if (dbItem == null) return NotFound();

                // Обновляю только нужные поля
                dbItem.Category = model.Category;
                dbItem.Text = model.Text;
                dbItem.IsActive = model.IsActive;

                await _db.SaveChangesAsync();

                if (IsAjax(Request))
                    return Json(new { ok = true, message = "Изменения сохранены." });
            }

           
            TempData["Success"] = "Сохранено.";
            return RedirectToAction(nameof(Index), new { q });
        }

        // Переключатель активности вкл/выкл
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id, string? q)
        {
            var item = await _db.MoodAdvices.FindAsync(id);
            if (item == null) return NotFound();

            item.IsActive = !item.IsActive;
            await _db.SaveChangesAsync();

            if (IsAjax(Request))
                return Json(new
                {
                    ok = true,
                    message = item.IsActive ? "Совет включён." : "Совет выключен."
                });

            TempData["Success"] = item.IsActive ? "Совет включён." : "Совет выключен.";
            return RedirectToAction(nameof(Index), new { q });
        }

        // Удаление совета
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? q)
        {
            var item = await _db.MoodAdvices.FindAsync(id);
            if (item == null) return NotFound();

            _db.MoodAdvices.Remove(item);
            await _db.SaveChangesAsync();

            if (IsAjax(Request))
                return Json(new { ok = true, message = "Совет удалён." });

            TempData["Success"] = "Совет удалён.";
            return RedirectToAction(nameof(Index), new { q });
        }

        // Внутренний метод фильтрации списка по поисковой строке q
        private IQueryable<MoodAdvice> Filtered(string? q)
        {
            var query = _db.MoodAdvices.AsQueryable();

            
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.Trim();

                query = query.Where(a =>
                    EF.Functions.Like(a.Category, $"%{qq}%") ||
                    EF.Functions.Like(a.Text, $"%{qq}%"));
            }

            return query;
        }
    }
}