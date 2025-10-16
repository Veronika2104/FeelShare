using System;
using System.Linq;
using System.Threading.Tasks;
using FeelShare.Web.Data;
using FeelShare.Web.Models;
using FeelShare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeelShare.Web.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        // Набор разрешённых реакций
        private static readonly (string key, string text, string emoji)[] Reactions =
        [
            ("heart", "Нравится", "❤️"),
            ("hug",   "Поддерживаю", "🤗"),
            ("smile", "Радуюсь", "😊"),
            ("wow",   "Ух ты", "😮"),
            ("up",    "Держись", "💪"),
            ("cry",    "Грустно", "😭"),
            
        ];

        public HomeController(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _um = um;
        }

        // Ключ для реакций/лайков (пользователь/гость)
        private async Task<string> GetReactKeyAsync()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                var u = await _um.GetUserAsync(User);
                return $"u:{u!.Id}";
            }
            if (!Request.Cookies.TryGetValue("fs_like", out var anon) || string.IsNullOrEmpty(anon))
            {
                anon = Guid.NewGuid().ToString("N");
                Response.Cookies.Append("fs_like", anon, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                });
            }
            return $"a:{anon}";
        }

        [HttpGet("/")]
        [AllowAnonymous]//доступ к странице без входа
        public async Task<IActionResult> Index(int? emotionId, int page = 1)
        {
            const int pageSize = 10; // постранично по 10 историй

            // 3.1. Эмоции для карточек
            var moods = await _db.Emotions
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync();

            // 3.2. Параметры контекста пользователя (реакции, владелец и т.п.)
            var reactKey = await GetReactKeyAsync();       // у кого-то userId, у гостя — cookie-идентификатор
            var currentUserId = _um.GetUserId(User);       // Id текущего пользователя, если вошёл
            var isAuth = User.Identity?.IsAuthenticated ?? false;

            // 3.3. Базовый запрос по историям
            IQueryable<PublicStory> q = _db.PublicStories
                .AsNoTracking()
                .Where(s => s.IsPublished);                // только опубликованные

            if (emotionId.HasValue)
                q = q.Where(s => s.EmotionId == emotionId.Value); // фильтр по эмоции (по кнопкам “чипсам”)

            var total = await q.CountAsync();              // всего записей для пагинации

            // 3.4. Выбираем только нужные поля + агрегаты (проекция)
            var items = await q
                .OrderByDescending(s => s.Id)              // новые выше
                .Skip((page - 1) * pageSize)               // страница N
                .Take(pageSize)
                .Select(s => new
                {
                    Id = s.Id,
                    EmotionId = s.EmotionId,
                    EmotionName = s.Emotion.Name,
                    EmotionIcon = s.Emotion.Icon,
                    Content = s.Content,
                    CreatedAtUtc = s.CreatedAtUtc,

                    // сгруппированные счётчики реакций
                    ReactionGroups = _db.StoryReactions
                        .Where(r => r.StoryId == s.Id)
                        .GroupBy(r => r.Reaction)
                        .Select(g => new { Key = g.Key, Count = g.Count() })
                        .ToList(),

                    // какие реакции поставил текущий пользователь/гость
                    MyReacts = _db.StoryReactions
                        .Where(r => r.StoryId == s.Id && r.ReactKey == reactKey)
                        .Select(r => r.Reaction)
                        .ToList(),

                    // число комментариев
                    CommentsCount = _db.StoryComments
                        .Count(c => c.StoryId == s.Id && !c.IsDeleted),

                    // 2 последних комментария для предпросмотра
                    Latest = _db.StoryComments
                        .Where(c => c.StoryId == s.Id && !c.IsDeleted)
                        .OrderByDescending(c => c.CreatedAtUtc)
                        .Take(2)
                        .Select(c => new CommentItemVM
                        {
                            Id = c.Id,
                            Author = c.User.DisplayName ?? c.User.Email!,
                            Content = c.Content,
                            CreatedAtUtc = c.CreatedAtUtc,
                            IsOwner = isAuth && c.UserId == currentUserId
                        })
                        .ToList()
                })
                .ToListAsync();

            // 3.5. Маппинг в ViewModel, удобную для Razor
            var stories = items.Select(x => new StoryListItemVM
            {
                Id = x.Id,
                EmotionId = x.EmotionId,
                EmotionName = x.EmotionName ?? "",
                EmotionIcon = x.EmotionIcon ?? "",
                Content = x.Content,
                CreatedAtUtc = x.CreatedAtUtc,
                ReactionCounts = x.ReactionGroups.ToDictionary(k => k.Key, v => v.Count),
                MyReactions = x.MyReacts ?? new List<string>(),
                CommentsCount = x.CommentsCount,
                LatestComments = x.Latest ?? new List<CommentItemVM>()
            }).ToList();

            var vm = new HomeIndexVM
            {
                Moods = moods,                  // карточки эмоций
                Stories = stories,              // лента
                SelectedEmotionId = emotionId,  // какой фильтр активен
                Page = page,
                HasMore = page * pageSize < total
            };

            return View(vm);
        }

        [Authorize] 
        [HttpPost("/stories/{id:int}/react/{reaction}", Name = "StoryReactToggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> React(int id, string reaction)
        {
            var allowed = Reactions.Select(r => r.key).ToHashSet();
            if (!allowed.Contains(reaction)) return BadRequest("Unknown reaction");

            var reactKey = $"u:{_um.GetUserId(User)}"; // раз логин обязателен — ключ всегда user

            var existing = await _db.StoryReactions
                .FirstOrDefaultAsync(x => x.StoryId == id && x.Reaction == reaction && x.ReactKey == reactKey);

            if (existing is null)
            {   // Добавляем реакцию
                _db.StoryReactions.Add(new StoryReaction { StoryId = id, Reaction = reaction, ReactKey = reactKey });
                try { await _db.SaveChangesAsync(); } catch { }
            }
            else
            {// Убираем реакцию (повторное нажатие снимает)
                _db.StoryReactions.Remove(existing);
                await _db.SaveChangesAsync();
            }

            // Если это AJAX — вернём свежие данные
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var counts = await _db.StoryReactions
                    .Where(r => r.StoryId == id)
                    .GroupBy(r => r.Reaction)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count);

                var my = await _db.StoryReactions
                    .Where(r => r.StoryId == id && r.ReactKey == reactKey)
                    .Select(r => r.Reaction)
                    .ToListAsync();

                return Json(new { ok = true, counts, my });
            }

            var back = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(back)) return Redirect(back);
            return RedirectToAction(nameof(Index));
        }

        // Создание анонимной истории
        [Authorize]
        [HttpPost("/stories/create", Name = "StoryCreate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStory(int emotionId, string content, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Напишите историю.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Index), new { emotionId });
            }

            var user = await _um.GetUserAsync(User);

            _db.PublicStories.Add(new PublicStory
            {
                EmotionId = emotionId,
                UserId = user!.Id,
                Content = content.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                IsPublished = true
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "История опубликована анонимно.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index), new { emotionId });
        }
        // УДАЛИТЬ СВОЮ ИСТОРИЮ
        [Authorize]
        [HttpPost("/stories/{id:int}/delete", Name = "StoryDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStory(int id, string? returnUrl = null)
        {
            var uid = _um.GetUserId(User);

            var story = await _db.PublicStories
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == uid);

            if (story is null) return NotFound();

            story.IsPublished = false;  // не стираем, а прячем из ленты
            await _db.SaveChangesAsync();

            

            TempData["Success"] = "История удалена.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var back = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(back)) return Redirect(back);

            return RedirectToAction("Index", "Profile");
        }
        // «Показать ещё» — подгрузка комментариев (AJAX, Partial)
        [HttpGet("/stories/{id:int}/comments", Name = "StoryCommentsChunk")]
        [AllowAnonymous]
        public async Task<IActionResult> CommentsChunk(int id, int skip = 0, int take = 5)
        {
            var currentUserId = _um.GetUserId(User);
            var isAuth = User.Identity?.IsAuthenticated ?? false;

            var comments = await _db.StoryComments
                .Where(c => c.StoryId == id && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .Select(c => new CommentItemVM
                {
                    Id = c.Id,
                    Author = c.User.DisplayName ?? c.User.Email!,
                    Content = c.Content,
                    CreatedAtUtc = c.CreatedAtUtc,
                    IsOwner = isAuth && c.UserId == currentUserId
                })
                .ToListAsync();

            return PartialView("_CommentsChunk", comments);
        }

        [Authorize]
        [HttpPost("/stories/{id:int}/comment", Name = "StoryCommentCreate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(int id, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { ok = false, message = "Комментарий не может быть пустым." });

                TempData["Error"] = "Комментарий не может быть пустым.";
                return Redirect(Request.Headers["Referer"].ToString());
            }

            var user = await _um.GetUserAsync(User);

            var cmt = new StoryComment
            {
                StoryId = id,
                UserId = user!.Id,
                Content = content.Trim()
            };
            _db.StoryComments.Add(cmt);
            await _db.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var currentUserId = _um.GetUserId(User);
                var vm = await _db.StoryComments
                    .Where(c => c.Id == cmt.Id)
                    .Select(c => new CommentItemVM
                    {
                        Id = c.Id,
                        Author = c.User.DisplayName ?? c.User.Email!,
                        Content = c.Content,
                        CreatedAtUtc = c.CreatedAtUtc,
                        IsOwner = c.UserId == currentUserId
                    })
                    .ToListAsync(); // _CommentsChunk ожидает List<CommentItemVM>

                return PartialView("_CommentsChunk", vm);
            }

            TempData["Success"] = "Комментарий добавлен.";
            return Redirect(Request.Headers["Referer"].ToString());
        }

        // Удалить комментарий (только автор)
        [Authorize]
        [HttpPost("/stories/comment/{id:int}/delete", Name = "StoryCommentDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var userId = _um.GetUserId(User);
            var c = await _db.StoryComments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (c is null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return NotFound(new { ok = false, message = "Комментарий не найден" });
                return NotFound();
            }

            var storyId = c.StoryId;
            c.IsDeleted = true;
            await _db.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, storyId, commentId = id });

            TempData["Success"] = "Комментарий удалён.";
            return Redirect(Request.Headers["Referer"].ToString());
        }
     
        // Страница эмоции (цитаты + форма сохранения в дневник)
        [HttpGet("/emotion/{slug}")]            // Маршрут с параметром {slug}
        [AllowAnonymous]                        // Доступен всем (даже без входа)
        public async Task<IActionResult> Emotion(string slug)
        {
            var emotion = await _db.Emotions
                .AsNoTracking()                 // читаем быстрее, без отслеживания
                .FirstOrDefaultAsync(e => e.Slug == slug); // ищем по slug

            if (emotion == null) return NotFound(); // 404, если нет такой эмоции

            var quotes = await _db.Quotes
                .Where(q => q.EmotionId == emotion.Id && q.IsActive) // только активные цитаты этой эмоции
                .OrderBy(q => Guid.NewGuid())     //  “случайность” 
                .Take(3)                          // показываем до трёх штук
                .AsNoTracking()
                .ToListAsync();

            ViewBag.RandomQuotes = quotes;        // передаём цитаты во View через ViewBag
            return View("Emotion", emotion);      // модель представления 
        }

        // Сохранить запись в личный дневник
        [HttpPost("/emotion/save")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Save(int emotionId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Напишите хоть пару слов ❤️";
                return Redirect(Request.Headers["Referer"].ToString());
            }

            var user = await _um.GetUserAsync(User);

            _db.JournalEntries.Add(new JournalEntry
            {
                UserId = user!.Id,
                EmotionId = emotionId,
                Content = content.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Сохранено в ваш дневник.";
            return RedirectToAction("Me", "Profile");
        }
    }
}