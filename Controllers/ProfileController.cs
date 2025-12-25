using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
    [Authorize] // профиль — только для авторизованных
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        public ProfileController(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _um = um;
        }

        // Главная панель профиля
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _um.GetUserAsync(User);
            var userId = user!.Id;

         
            var moods = await _db.Emotions
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync();

            // блок статистики (сколько записей, историй, комментов)
            var stats = new ProfileStatsVM
            {
                MyEntries = await _db.JournalEntries.CountAsync(j => j.UserId == userId),
                MyStories = await _db.PublicStories.CountAsync(s => s.UserId == userId && s.IsPublished),
                MyComments = await _db.StoryComments.CountAsync(c => c.UserId == userId && !c.IsDeleted),
                FeedbackToMe = await _db.StoryComments.CountAsync(c => !c.IsDeleted && c.Story.UserId == userId && c.UserId != userId)
            };

            // последние 5 записей дневника
            var latestEntries = await _db.JournalEntries
                .Include(j => j.Emotion)
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAtUtc)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            // мои последние 5 комментариев 
            var latestMyComments = await _db.StoryComments
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(5)
                .Select(c => new MyCommentRowVM
                {
                    StoryId = c.StoryId,
                    CommentId = c.Id,
                    StoryPreview = c.Story.Content.Length > 120 ? c.Story.Content.Substring(0, 120) + "…" : c.Story.Content,
                    CommentPreview = c.Content.Length > 160 ? c.Content.Substring(0, 160) + "…" : c.Content,
                    CreatedAtLocal = c.CreatedAtUtc.ToLocalTime()
                })
                .AsNoTracking()
                .ToListAsync();

            // последние 5 комментариев от других людей к моим историям
            var latestFeedback = await _db.StoryComments
                .Where(c => c.Story.UserId == userId && c.UserId != userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(5)
                .Select(c => new FeedbackRowVM
                {
                    StoryId = c.StoryId,
                    CommentId = c.Id,
                    From = c.User.DisplayName ?? c.User.Email!,
                    CommentPreview = c.Content.Length > 160 ? c.Content.Substring(0, 160) + "…" : c.Content,
                    CreatedAtLocal = c.CreatedAtUtc.ToLocalTime()
                })
                .AsNoTracking()
                .ToListAsync();

            // последние 5 моих опубликованных историй
            var latestMyStories = await _db.PublicStories
                .Where(s => s.UserId == userId && s.IsPublished)
                .OrderByDescending(s => s.Id)
                .Take(5)
                .Select(s => new MyStoryRowVM
                {
                    Id = s.Id,
                    EmotionName = s.Emotion.Name,
                    EmotionIcon = s.Emotion.Icon,
                    Preview = s.Content.Length > 200 ? s.Content.Substring(0, 200) + "…" : s.Content,
                    CreatedAtLocal = s.CreatedAtUtc,
                    CommentsCount = _db.StoryComments.Count(c => c.StoryId == s.Id && !c.IsDeleted),
                    ReactionsCount = _db.StoryReactions.Count(r => r.StoryId == s.Id)
                })
                .AsNoTracking()
                .ToListAsync();

        
            var vm = new ProfileIndexVM
            {
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email! : user.DisplayName!,
                Moods = moods,
                Stats = stats,
                LatestEntries = latestEntries,
                LatestMyComments = latestMyComments,
                LatestFeedback = latestFeedback,
                LatestMyStories = latestMyStories
            };

            return View(vm);
        }

        // Полный список записей дневника
        [HttpGet]
        public async Task<IActionResult> Me()
        {
            var user = await _um.GetUserAsync(User);

            var items = await _db.JournalEntries
                .Include(j => j.Emotion)
                .Where(j => j.UserId == user!.Id)
                .OrderByDescending(j => j.CreatedAtUtc)
                .AsNoTracking()
                .ToListAsync();

            return View(items);
        }

        // Страница с графиком настроения по месяцам + совет на сегодня
        [HttpGet("/profile/emotions")]
        public async Task<IActionResult> Emotions(int? year = null, int? month = null)
        {
            var uid = _um.GetUserId(User);

            // беру текущие год/месяц по локальному времени
            var nowLocal = DateTime.Now;

            int y = year ?? nowLocal.Year;
            int m = month ?? nowLocal.Month;
            if (m < 1) m = 1;
            if (m > 12) m = 12;

           
            var fromLocal = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Local);
            var toLocal = fromLocal.AddMonths(1);

            var fromUtc = fromLocal.ToUniversalTime();
            var toUtc = toLocal.ToUniversalTime();

            // сохраняю выбранные значения для селектов
            ViewBag.SelectedYear = y;
            ViewBag.SelectedMonth = m;

            // вычисляю диапазон годов от первого замера до текущего
            var firstSurveyUtc = await _db.MoodSurveys
                .Where(s => s.UserId == uid)
                .OrderBy(s => s.CreatedAtUtc)
                .Select(s => (DateTime?)s.CreatedAtUtc)
                .FirstOrDefaultAsync();

            var firstYear = firstSurveyUtc?.ToLocalTime().Year ?? DateTime.Now.Year;
            var currentYear = DateTime.Now.Year;

            ViewBag.Years = Enumerable.Range(firstYear, currentYear - firstYear + 1).ToList();

            // список месяцев на русском
            var ru = new CultureInfo("ru-RU");
            ViewBag.Months = Enumerable.Range(1, 12)
                .Select(mm => new
                {
                    Value = mm,
                    Text = char.ToUpper(ru.DateTimeFormat.GetMonthName(mm)[0]) +
                           ru.DateTimeFormat.GetMonthName(mm).Substring(1)
                })
                .ToList();

            // беру все оценки за месяц
            var rows = await _db.MoodSurveyItems
                .AsNoTracking()
                .Where(x => x.Survey.UserId == uid &&
                            x.Survey.CreatedAtUtc >= fromUtc &&
                            x.Survey.CreatedAtUtc < toUtc)
                .Select(x => new
                {
                    x.EmotionId,
                    x.Score,
                    CreatedAtUtc = x.Survey.CreatedAtUtc
                })
                .ToListAsync();

            // группирую по локальному дню, чтобы не было сдвига дня из-за UTC
            var rowsLocalDay = rows.Select(r => new
            {
                r.EmotionId,
                r.Score,
                DayLocal = DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc).ToLocalTime().Date
            }).ToList();

            // X-ось графика = дни, когда были замеры
            var days = rowsLocalDay.Select(r => r.DayLocal).Distinct().OrderBy(d => d).ToList();
            var labels = days.Select(d => d.ToString("dd.MM")).ToList();

            // emotionId -> day -> avg
            var avgByEmotionDay = rowsLocalDay
                .GroupBy(r => r.EmotionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.DayLocal)
                          .ToDictionary(gg => gg.Key, gg => gg.Average(v => v.Score))
                );

            var emotions = await _db.Emotions
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync();

         
            var series = emotions
                .Where(e => avgByEmotionDay.ContainsKey(e.Id))
                .Select(e =>
                {
                    var map = avgByEmotionDay[e.Id];
                    var values = days.Select(day =>
                        map.TryGetValue(day, out var avg) ? (double?)Math.Round(avg, 2) : null
                    ).ToList();

                    return new
                    {
                        name = $"{e.Icon} {e.Name}",
                        values
                    };
                })
                .ToList();

            // средняя оценка "сегодня" 
            var todayLocal = DateTime.Now.Date;
            var tomorrowLocal = todayLocal.AddDays(1);

            var todayFromUtc = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
            var todayToUtc = DateTime.SpecifyKind(tomorrowLocal, DateTimeKind.Local).ToUniversalTime();

            var todayScores = await _db.MoodSurveys
                .AsNoTracking()
                .Where(s => s.UserId == uid &&
                            s.CreatedAtUtc >= todayFromUtc &&
                            s.CreatedAtUtc < todayToUtc)
                .SelectMany(s => s.Items.Select(i => i.Score))
                .ToListAsync();

            double? todayAvg = todayScores.Count > 0
                ? Math.Round(todayScores.Average(), 2)
                : (double?)null;

            // совет по категории (bad/neutral/good) на основе среднего за сегодня
            string? advice = null;
            if (todayAvg.HasValue)
            {
                var cat = todayAvg.Value < 2.0 ? "bad" : (todayAvg.Value < 4.0 ? "neutral" : "good");
                advice = await _db.MoodAdvices
                    .AsNoTracking()
                    .Where(a => a.IsActive && a.Category == cat)
                    .OrderBy(a => a.Id)
                    .Select(a => a.Text)
                    .FirstOrDefaultAsync();
            }

            // данные для View 
            ViewBag.Labels = labels;
            ViewBag.Series = series;
            ViewBag.TodayAvg = todayAvg;
            ViewBag.Advice = advice;

            return View();
        }

       
        private static DateTime ParseMonthOrDefault(string? month, DateTime fallbackLocal)
        {
            if (!string.IsNullOrWhiteSpace(month) &&
                DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return new DateTime(parsed.Year, parsed.Month, 1);
            }

            return new DateTime(fallbackLocal.Year, fallbackLocal.Month, 1);
        }

        //МОДАЛКА ЗАПИСИ ДНЕВНИКА 

        // тело модалки (Partial), чтобы открыть запись без перехода на отдельную страницу
        [HttpGet("/profile/entry/{id:int}")]
        public async Task<IActionResult> Entry(int id)
        {
            var user = await _um.GetUserAsync(User);

            var entry = await _db.JournalEntries
                .Include(j => j.Emotion)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == user!.Id);

            if (entry == null) return NotFound();

            return PartialView("~/Views/Profile/_EntryModalBody.cshtml", new EntryModalVM(entry));
        }

        // VM для редактирования записи post
        public class EntryEditVM
        {
            [Required] public int Id { get; set; }
            [Required, MinLength(1), DataType(DataType.MultilineText)]
            public string Content { get; set; } = null!;
        }

        // обновление текста записи дневника
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(EntryEditVM vm)
        {
            if (!ModelState.IsValid) return BadRequest("Текст обязателен.");

            var user = await _um.GetUserAsync(User);
            var entry = await _db.JournalEntries.FirstOrDefaultAsync(j => j.Id == vm.Id && j.UserId == user!.Id);
            if (entry == null) return NotFound();

            entry.Content = vm.Content.Trim();
            entry.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Изменения сохранены.";
            return RedirectToAction(nameof(Me));
        }

        // удаление записи дневника
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _um.GetUserAsync(User);

            var entry = await _db.JournalEntries.FirstOrDefaultAsync(j => j.Id == id && j.UserId == user!.Id);
            if (entry == null) return NotFound();

            _db.JournalEntries.Remove(entry);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Запись удалена.";
            return RedirectToAction(nameof(Me));
        }
    }

    // VM для модалки просмотра записи 
    public class EntryModalVM
    {
        public int Id { get; init; }
        public string EmotionName { get; init; }
        public string EmotionIcon { get; init; }
        public string Content { get; init; }
        public DateTime CreatedAtLocal { get; init; }
        public DateTime? UpdatedAtLocal { get; init; }

        public EntryModalVM(JournalEntry e)
        {
            Id = e.Id;
            EmotionName = e.Emotion.Name;
            EmotionIcon = e.Emotion.Icon;
            Content = e.Content;
            CreatedAtLocal = e.CreatedAtUtc.ToLocalTime();
            UpdatedAtLocal = e.UpdatedAtUtc?.ToLocalTime();
        }
    }
}
