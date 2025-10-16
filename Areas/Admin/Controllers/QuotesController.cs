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
            // Наполняем выпадающий список эмоций для фильтра в представлении
            ViewBag.Emotions = new SelectList(
                await _db.Emotions.OrderBy(e => e.Id).ToListAsync(), // получаем эмоции
                "Id",   // value
                "Name", // текст
                emotionId // выбранное значение по умолчанию
            );

            // Базовый запрос к таблице цитат, сразу подтягиваем Emotion
            var query = _db.Quotes.Include(x => x.Emotion).AsQueryable();

            // Фильтр по эмоции (если выбрана)
            if (emotionId.HasValue)
                query = query.Where(x => x.EmotionId == emotionId);

            // Поиск по тексту (если что-то ввели)
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => x.Text.Contains(q));

            // Упорядочиваем и выполняем запрос к БД
            var items = await query
                .OrderBy(x => x.EmotionId)  // группируем по эмоциям
                .ThenBy(x => x.SortOrder)   // ручной порядок внутри эмоции
                .ThenBy(x => x.Id)          // стабильность сортировки
                .ToListAsync();

            return View(items); // отдаём список представлению
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? emotionId)
        {
            // Снова заполняем список эмоций для выпадающего списка
            ViewBag.Emotions = new SelectList(
                await _db.Emotions.OrderBy(e => e.Id).ToListAsync(), "Id", "Name", emotionId);

            // Отдаём форму с дефолтами
            return View(new Quote { IsActive = true, SortOrder = 0, EmotionId = emotionId ?? 0 });
        }
        // Создание цитаты
        [HttpPost]
        [ValidateAntiForgeryToken] // защита от CSRF
        public async Task<IActionResult> Create(Quote model, string? saveAction)
        {
            // Нормализуем ввод
            model.Text = model.Text?.Trim() ?? "";
            model.Author = string.IsNullOrWhiteSpace(model.Author) ? null : model.Author.Trim();

            // Если валидация модели не прошла — вернём форму с ошибками
            if (!ModelState.IsValid)
            {
                ViewBag.Emotions = new SelectList(
                    await _db.Emotions.OrderBy(e => e.Id).ToListAsync(), "Id", "Name", model.EmotionId);
                TempData["Error"] = "Проверьте поля ниже.";
                return View(model);
            }

            try
            {
                // СОХРАНЕНИЕ В БД 
                _db.Quotes.Add(model);          // ставим сущность в состояние "Added"
                await _db.SaveChangesAsync();   // отправляем INSERT в БД
                TempData["Success"] = "Цитата добавлена.";

                // Если нажали "Сохранить и добавить ещё"
                if (string.Equals(saveAction, "save-add", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction(nameof(Create), new { emotionId = model.EmotionId });

                // Иначе — в список, оставив выбранную эмоцию отфильтрованной
                return RedirectToAction(nameof(Index), new { emotionId = model.EmotionId });
            }
            catch (DbUpdateException ex)
            {
                // Ошибки БД (например, нарушение ограничений)
                TempData["Error"] = "Ошибка БД: " + ex.GetBaseException().Message;
                ViewBag.Emotions = new SelectList(
                    await _db.Emotions.OrderBy(e => e.Id).ToListAsync(), "Id", "Name", model.EmotionId);
                return View(model);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var q = await _db.Quotes.FindAsync(id); // быстрый поиск по ключу
            if (q == null) return NotFound();

            _db.Quotes.Remove(q);       // помечаем сущность как "Deleted"
            await _db.SaveChangesAsync(); // выполняется DELETE в БД

            TempData["Success"] = "Цитата удалена.";
            return RedirectToAction(nameof(Index));
        }
    }
}