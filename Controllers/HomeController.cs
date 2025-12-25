
using FeelShare.Web.Data;
using FeelShare.Web.Models;
using FeelShare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Text;

namespace FeelShare.Web.Controllers
{
    [AllowAnonymous] // по умолчанию страницы доступны всем, но отдельные методы требуют [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        public HomeController(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _um = um;
        }

        // Разрешённые реакции (ключ — то, что хранится в БД)
        private static readonly (string key, string text, string emoji)[] Reactions =
        [
            ("heart", "Нравится", "❤️"),
            ("hug",   "Поддерживаю", "🤗"),
            ("smile", "Радуюсь", "😊"),
            ("wow",   "Ух ты", "😮"),
            ("up",    "Держись", "💪"),
            ("cry",   "Грустно", "😭"),
        ];

        // Ключ для реакций (если пользователь залогинен — u:ID, если гость — a:cookie)
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

        // Ключ для жалоб 
        private async Task<string> GetReportKeyAsync()
        {
            if (User.Identity?.IsAuthenticated ?? false)
                return $"u:{_um.GetUserId(User)}";

            if (!Request.Cookies.TryGetValue("fs_report", out var anon) || string.IsNullOrEmpty(anon))
            {
                anon = Guid.NewGuid().ToString("N");
                Response.Cookies.Append("fs_report", anon, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                });
            }

            return $"a:{anon}";
        }

        // Нормализация текста для авто-модерации (чтобы ловить обходы “нннарк0т@”)
        private static string NormalizeForModeration(string text)
        {
            text = (text ?? string.Empty).ToLowerInvariant();

            // 1) Убираю растяжения символов: “нннаркота” -> “наркота”
            text = Regex.Replace(text, @"(.)\1{2,}", "$1");

            // 2) Подмена похожих символов (цифры/знаки → буквы)
            var map = new Dictionary<char, char>
            {
                ['0'] = 'о',
                ['@'] = 'а',
                ['3'] = 'з',
                ['1'] = 'и',
                ['!'] = 'и',
                ['5'] = 'с',
            };

            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
                sb.Append(map.TryGetValue(ch, out var repl) ? repl : ch);

            text = sb.ToString();

            // 3) Убираю всё не буквы/цифры
            text = Regex.Replace(text, @"[\W_]+", "");

            return text;
        }

        // авто-оценка риска для истории: Published / Pending / Rejected
        private static (ModerationStatus status, string? note) DetectRisk(string originalText)
        {
            var norm = NormalizeForModeration(originalText);

            // Жёстко запрещённое сразу Rejected
            string[] hardRejectRoots =
            {
                "педоф", "детскпорн", "cp", "childporn",
                "террор", "взорв", "бомб", "расчлен","убил",
                "инструкц", "каксделатьбомб"
            };

            if (hardRejectRoots.Any(r => norm.Contains(r)))
                return (ModerationStatus.Rejected, "Авто: жёсткий запрет (ключевые корни)");

            // Подозрительное Pending 
            string[] pendingRoots =
            {
                "убий", "убивать", "нож", "зарез",
                "суицид", "самоуб", "повеш", "порезалсеб",
                "наркот", "нарк", "кокс", "героин",
                "изнасил", "насил", "похит", "шантаж",
                "оруж", "пистолет", "автомат"
            };

            if (pendingRoots.Any(r => norm.Contains(r)))
                return (ModerationStatus.Pending, "Авто: повышенный риск (ключевые корни)");

            return (ModerationStatus.Published, null);
        }

        // Главная страница: лента историй + фильтры + пагинация курсором 
        [HttpGet("/")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            int? emotionId,
            string? period,
            int? weekOffset,
            string? afterCreated,
            int? afterId)
        {
            const int PageSize = 10;

            // Карточки эмоций сверху
            var moods = await _db.Emotions
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync();

            // Контекст текущего пользователя
            var reactKey = await GetReactKeyAsync();
            var currentUserId = _um.GetUserId(User);
            var isAuth = User.Identity?.IsAuthenticated ?? false;

            // Базовый запрос: только опубликованные и прошедшие модерацию
            IQueryable<PublicStory> q = _db.PublicStories
                .AsNoTracking()
                .Where(s => s.IsPublished && s.ModerationStatus == ModerationStatus.Published);

            // Фильтр по эмоции
            if (emotionId.HasValue)
                q = q.Where(s => s.EmotionId == emotionId.Value);

            // Фильтр по периоду
            var nowUtc = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(period))
            {
                if (period == "today")
                {
                    var fromTodayUtc = nowUtc.Date;
                    q = q.Where(s => s.CreatedAtUtc >= fromTodayUtc);
                }
                else if (period == "7d")
                {
                    var from7dUtc = nowUtc.AddDays(-7);
                    q = q.Where(s => s.CreatedAtUtc >= from7dUtc);
                }
                else if (period == "30d")
                {
                    var from30dUtc = nowUtc.AddDays(-30);
                    q = q.Where(s => s.CreatedAtUtc >= from30dUtc);
                }
                else if (period == "year")
                {
                    var fromUtc = nowUtc.AddYears(-1);
                    q = q.Where(s => s.CreatedAtUtc >= fromUtc);
                }
                else if (period == "week")
                {
                    int w = Math.Max(0, weekOffset ?? 0);
                    var (fromUtc, toUtc) = GetIsoWeekRangeUtc(nowUtc, w);
                    q = q.Where(s => s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc < toUtc);
                }
            }

            // Сортировка ленты
            q = q.OrderByDescending(s => s.CreatedAtUtc)
                 .ThenByDescending(s => s.Id);

            // Cursor pagination
            if (DateTime.TryParse(afterCreated, null, System.Globalization.DateTimeStyles.RoundtripKind, out var afterCreatedUtc)
                && afterId.HasValue)
            {
                q = q.Where(s =>
                    (s.CreatedAtUtc < afterCreatedUtc) ||
                    (s.CreatedAtUtc == afterCreatedUtc && s.Id < afterId.Value));
            }

            
            var pageKeys = await q
                .Select(s => new { s.Id, s.CreatedAtUtc })
                .Take(PageSize + 1)
                .ToListAsync();

            bool hasMore = pageKeys.Count > PageSize;
            if (hasMore) pageKeys.RemoveAt(PageSize);

            // Подготовка следующего курсора
            string? nextAfterCreated = null;
            int? nextAfterId = null;
            if (hasMore && pageKeys.Count > 0)
            {
                var last = pageKeys[^1];
                nextAfterCreated = last.CreatedAtUtc.ToString("o");
                nextAfterId = last.Id;
            }

            var pageIds = pageKeys.Select(x => x.Id).ToList();

            // 2) Теперь беру детали по этим Id (реакции, комменты, эмодзи эмоции)
            var items = await _db.PublicStories
                .AsNoTracking()
                .Where(s => pageIds.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.EmotionId,
                    EmotionName = s.Emotion.Name,
                    EmotionIcon = s.Emotion.Icon,
                    s.Content,
                    s.CreatedAtUtc,

                    ReactionGroups = _db.StoryReactions
                        .Where(r => r.StoryId == s.Id)
                        .GroupBy(r => r.Reaction)
                        .Select(g => new { Key = g.Key, Count = g.Count() })
                        .ToList(),

                    MyReacts = _db.StoryReactions
                        .Where(r => r.StoryId == s.Id && r.ReactKey == reactKey)
                        .Select(r => r.Reaction)
                        .ToList(),

                    CommentsCount = _db.StoryComments
                        .Count(c => c.StoryId == s.Id && !c.IsDeleted),

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

            // Возвращаю порядок как в pageIds (иначе SQL IN может вернуть в другом порядке)
            var order = pageIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
            items = items.OrderBy(x => order[x.Id]).ToList();

            
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
                Moods = moods,
                Stories = stories,
                SelectedEmotionId = emotionId,
                HasMore = hasMore,
                NextAfterCreated = nextAfterCreated,
                NextAfterId = nextAfterId,
                Period = period,
                WeekOffset = weekOffset
            };

            return View(vm);
        }

        // Реакции 
        [Authorize]
        [HttpPost("/stories/{id:int}/react/{reaction}", Name = "StoryReactToggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> React(int id, string reaction)
        {
            // Разрешаю только те реакции, что описаны в массиве Reactions
            var allowed = Reactions.Select(r => r.key).ToHashSet();
            if (!allowed.Contains(reaction)) return BadRequest("Unknown reaction");

            // Тут реакция доступна только авторизованным
            var reactKey = $"u:{_um.GetUserId(User)}";

            var existing = await _db.StoryReactions
                .FirstOrDefaultAsync(x => x.StoryId == id && x.Reaction == reaction && x.ReactKey == reactKey);

            if (existing is null)
            {
                _db.StoryReactions.Add(new StoryReaction { StoryId = id, Reaction = reaction, ReactKey = reactKey });
                try { await _db.SaveChangesAsync(); } catch { }
            }
            else
            {
                _db.StoryReactions.Remove(existing);
                await _db.SaveChangesAsync();
            }

            // Если запрос AJAX — отдаю JSON с обновлёнными счётчиками
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

            // Иначе просто возвращаю обратно на страницу
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
            var trimmed = (content ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                TempData["Error"] = "Напишите историю.";
                return RedirectAfterCreate(emotionId, returnUrl);
            }

            // Авто-оценка риска
            var (status, note) = DetectRisk(trimmed);

            var user = await _um.GetUserAsync(User);

            var story = new PublicStory
            {
                EmotionId = emotionId,
                UserId = user!.Id,
                Content = trimmed,
                CreatedAtUtc = DateTime.UtcNow,

                ModerationStatus = status,
                ModerationNote = note,

                // В ленту попадает только Published
                IsPublished = (status == ModerationStatus.Published)
            };

            _db.PublicStories.Add(story);
            await _db.SaveChangesAsync();

            // Сообщение пользователю
            if (status == ModerationStatus.Published)
                TempData["Success"] = "История опубликована анонимно.";
            else if (status == ModerationStatus.Pending)
                TempData["Success"] = "Спасибо! История отправлена на проверку и появится после модерации.";
            else
                TempData["Error"] = "История нарушает правила сообщества и не может быть опубликована.";

            return RedirectAfterCreate(emotionId, returnUrl);
        }

     
        private IActionResult RedirectAfterCreate(int emotionId, string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index), new { emotionId });
        }

        //  Жалоба на историю 
        [HttpPost("/stories/{id:int}/report", Name = "StoryReport")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int id, string? reason, string? returnUrl)
        {
            var story = await _db.PublicStories.FirstOrDefaultAsync(s => s.Id == id);
            if (story == null) return NotFound();

            var reportKey = await GetReportKeyAsync();

            // Если уже жаловался  не даю накрутить
            bool exists = await _db.StoryReports.AnyAsync(r => r.StoryId == id && r.ReportKey == reportKey);
            if (exists)
            {
                TempData["Error"] = "Вы уже отправляли жалобу на эту историю.";
                return RedirectAfterCreate(story.EmotionId, returnUrl);
            }

            // Нормализую reason
            var r = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (r != null && r.Length > 40) r = r.Substring(0, 40);

            _db.StoryReports.Add(new StoryReport
            {
                StoryId = id,
                ReportKey = reportKey,
                Reason = r,
                CreatedAtUtc = DateTime.UtcNow
            });

            story.ReportsCount += 1;

            // Авто-скрытие по порогу жалоб
            const int AutoHideThreshold = 3;
            if (story.ReportsCount >= AutoHideThreshold && story.ModerationStatus == ModerationStatus.Published)
            {
                story.ModerationStatus = ModerationStatus.Pending;
                story.ModerationNote = $"Авто: {story.ReportsCount} жалоб";
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Вы уже отправляли жалобу на эту историю.";
                return RedirectAfterCreate(story.EmotionId, returnUrl);
            }

            TempData["Success"] = "Жалоба отправлена. Спасибо!";
            return RedirectAfterCreate(story.EmotionId, returnUrl);
        }

        //  RedirectAfterCreate (emotionId nullable + referrer)
        private IActionResult RedirectAfterCreate(int? emotionId, string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var back = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(back))
                return Redirect(back);

            if (emotionId.HasValue)
                return RedirectToAction(nameof(Index), new { emotionId = emotionId.Value });

            return RedirectToAction(nameof(Index));
        }

        //  Удаление своей истории 
        [Authorize]
        [HttpPost("/stories/{id:int}/delete", Name = "StoryDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStory(int id, string? returnUrl = null)
        {
            var uid = _um.GetUserId(User);

            var story = await _db.PublicStories
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == uid);

            if (story is null) return NotFound();

            // Не удаляю из БД, а просто убираю из ленты
            story.IsPublished = false;
            await _db.SaveChangesAsync();

            TempData["Success"] = "История удалена.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var back = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(back)) return Redirect(back);

            return RedirectToAction("Index", "Profile");
        }

        //  Комментарии: подгрузка
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

        // Создать комментарий
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

            // Если AJAX — возвращаю частичное представление с новым комментом
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
                    .ToListAsync();

                return PartialView("_CommentsChunk", vm);
            }

            TempData["Success"] = "Комментарий добавлен.";
            return Redirect(Request.Headers["Referer"].ToString());
        }

        // Удалить комментарий (только свой) 
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

        // Страница эмоции: цитаты + дневник + панель упражнений
        [HttpGet("/emotion/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> Emotion(string slug)
        {
            
            string panelTagName = "Anxiety";
            string panelTagRu = "Тревога";

            switch ((slug ?? "").ToLowerInvariant())
            {
                case "trevozhno":
                case "anxiety":
                    panelTagName = "Anxiety"; panelTagRu = "Тревога"; break;

                case "noch":
                case "night":
                    panelTagName = "Night"; panelTagRu = "Ночь"; break;

                case "spokoino":
                case "calmer":
                    panelTagName = "Calmer"; panelTagRu = "Спокойнее"; break;
            }

            ViewBag.PanelTagName = panelTagName;
            ViewBag.PanelTagRu = panelTagRu;

            // Ищу эмоцию по slug
            var emotion = await _db.Emotions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Slug == slug);

            if (emotion == null) return NotFound();

            // Беру 3 случайные активные цитаты этой эмоции
            var quotes = await _db.Quotes
                .Where(q => q.EmotionId == emotion.Id && q.IsActive)
                .OrderBy(q => Guid.NewGuid())
                .Take(3)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.RandomQuotes = quotes;

            return View("Emotion", emotion);
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

        // ISO-неделя для фильтра week
        private static (DateTime fromUtc, DateTime toUtc) GetIsoWeekRangeUtc(DateTime nowUtc, int offset)
        {
            int delta = ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var mondayThisWeek = nowUtc.Date.AddDays(-delta);
            var from = mondayThisWeek.AddDays(-7 * Math.Max(0, offset));
            var to = from.AddDays(7);
            return (from, to);
        }
    }
}
