using FeelShare.Web.Data;
using FeelShare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeelShare.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ModerationController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        public ModerationController(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _um = um;
        }

        // Очередь модерации:
        // - pending: всё, что ждёт проверки
        // - reported: уже опубликованные, но с жалобами
        public async Task<IActionResult> Index(string tab = "pending", int page = 1)
        {
            const int PageSize = 20;
            page = Math.Max(1, page);

            // Базовый запрос: истории + Emotion 
            IQueryable<PublicStory> q = _db.PublicStories
                .Include(s => s.Emotion)
                .AsNoTracking();

            if (tab == "reported")
            {
                // Уже опубликовано + есть жалобы
                q = q.Where(s => s.ModerationStatus == ModerationStatus.Published && s.ReportsCount > 0)
                     .OrderByDescending(s => s.ReportsCount)
                     .ThenByDescending(s => s.CreatedAtUtc);
            }
            else // pending
            {
                // Только то, что ожидает модерации
                q = q.Where(s => s.ModerationStatus == ModerationStatus.Pending)
                     .OrderByDescending(s => s.ReportsCount)
                     .ThenByDescending(s => s.CreatedAtUtc);
            }

            // Пагинация: считаю общее и беру текущую страницу
            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            // Передаю параметры в представление 
            ViewBag.Tab = tab;
            ViewBag.Page = page;
            ViewBag.Total = total;
            ViewBag.PageSize = PageSize;

            return View(items);
        }

        // Одобрить историю: публикуем, ставим метаданные модерации
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? note, string? tab, int page = 1)
        {
            var story = await _db.PublicStories.FirstOrDefaultAsync(s => s.Id == id);
            if (story == null) return NotFound();

            story.ModerationStatus = ModerationStatus.Published;
            story.IsPublished = true; //  чтобы точно попало в ленту
            story.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            story.ModeratedAtUtc = DateTime.UtcNow;
            story.ModeratedByUserId = _um.GetUserId(User);

           
            _db.Entry(story).State = EntityState.Modified;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Одобрено.";

            return RedirectToAction(nameof(Index), new { tab = tab ?? "pending", page });
        }

        // Отклонить историю: убираем из публикации и фиксируем причину
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? note, string? tab, int page = 1)
        {
            var story = await _db.PublicStories.FirstOrDefaultAsync(s => s.Id == id);
            if (story == null) return NotFound();

            story.ModerationStatus = ModerationStatus.Rejected;
            story.IsPublished = false; // чтобы точно не было в ленте
            story.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            story.ModeratedAtUtc = DateTime.UtcNow;
            story.ModeratedByUserId = _um.GetUserId(User);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Отклонено.";

            return RedirectToAction(nameof(Index), new { tab = tab ?? "pending", page });
        }

        // Скрыть из жалоб: переводим в Pending, чтобы снова попало в очередь
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id, string? note, string? tab, int page = 1)
        {
            var story = await _db.PublicStories.FirstOrDefaultAsync(s => s.Id == id);
            if (story == null) return NotFound();

            story.ModerationStatus = ModerationStatus.Pending;

            
            story.ModerationNote = string.IsNullOrWhiteSpace(note) ? "Скрыто из ленты" : note.Trim();
            story.ModeratedAtUtc = DateTime.UtcNow;
            story.ModeratedByUserId = _um.GetUserId(User);

            _db.Entry(story).State = EntityState.Modified;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Скрыто (Pending).";

            return RedirectToAction(nameof(Index), new { tab = tab ?? "reported", page });
        }
    }
}