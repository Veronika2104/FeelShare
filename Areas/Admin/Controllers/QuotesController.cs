using FeelShare.Web.Data;
using FeelShare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FeelShare.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class QuotesController(AppDbContext db) : Controller
    {
        private readonly AppDbContext _db = db;

        // Список цитат + фильтр по эмоции и поиск по тексту
        [HttpGet]
        public async Task<IActionResult> Index(int? emotionId, string? q)
        {
            // Заполняю список эмоций для фильтра 
            ViewBag.Emotions = new SelectList(
                await _db.Emotions.OrderBy(e => e.Id).ToListAsync(),
                "Id",
                "Name",
                emotionId
            );

            // Базовый запрос к цитатам 
            var query = _db.Quotes.Include(x => x.Emotion).AsQueryable();

            // Фильтр по эмоции
            if (emotionId.HasValue)
                query = query.Where(x => x.EmotionId == emotionId);

            // Поиск по тексту
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => x.Text.Contains(q));

            // Сортировка: группирую по эмоциям
            var items = await query
                .OrderBy(x => x.EmotionId)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return View(items);
        }

        // Форма создания
        [HttpGet]
        public async Task<IActionResult> Create(int? emotionId)
        {
            ViewBag.Emotions = new SelectList(
                await _db.Emotions.OrderBy(e => e.Id).ToListAsync(),
                "Id",
                "Name",
                emotionId
            );

            // По умолчанию активная цитата и порядок = 0
            return View(new Quote
            {
                IsActive = true,
                SortOrder = 0,
                EmotionId = emotionId ?? 0
            });
        }

        // Создание цитаты (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Quote model, string? saveAction)
        {
            // Нормализую ввод, чтобы в базе не было лишних пробелов
            model.Text = model.Text?.Trim() ?? "";
            model.Author = string.IsNullOrWhiteSpace(model.Author) ? null : model.Author.Trim();

            // Если модель невалидна — возвращаю форму и заново наполняю 
            if (!ModelState.IsValid)
            {
                ViewBag.Emotions = new SelectList(
                    await _db.Emotions.OrderBy(e => e.Id).ToListAsync(),
                    "Id",
                    "Name",
                    model.EmotionId
                );
                TempData["Error"] = "Проверьте поля ниже.";
                return View(model);
            }

            try
            {
                _db.Quotes.Add(model);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Цитата добавлена.";

                // Кнопка "Сохранить и добавить ещё"
                if (string.Equals(saveAction, "save-add", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction(nameof(Create), new { emotionId = model.EmotionId });

                // Иначе возвращаюсь в список (с фильтром по эмоции)
                return RedirectToAction(nameof(Index), new { emotionId = model.EmotionId });
            }
            catch (DbUpdateException ex)
            {
                TempData["Error"] = "Ошибка БД: " + ex.GetBaseException().Message;

                ViewBag.Emotions = new SelectList(
                    await _db.Emotions.OrderBy(e => e.Id).ToListAsync(),
                    "Id",
                    "Name",
                    model.EmotionId
                );

                return View(model);
            }
        }

        // Удаление
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var q = await _db.Quotes.FindAsync(id);
            if (q == null) return NotFound();

            _db.Quotes.Remove(q);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Цитата удалена.";
            return RedirectToAction(nameof(Index));
        }
    }
}
