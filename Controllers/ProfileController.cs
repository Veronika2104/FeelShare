using System.ComponentModel.DataAnnotations;
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
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        public ProfileController(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _um = um;
        }

        // Панель профиля
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _um.GetUserAsync(User);
            var userId = user!.Id;

            var moods = await _db.Emotions
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .ToListAsync();

            var stats = new ProfileStatsVM
            {
                MyEntries = await _db.JournalEntries.CountAsync(j => j.UserId == userId),
                MyStories = await _db.PublicStories.CountAsync(s => s.UserId == userId && s.IsPublished),
                MyComments = await _db.StoryComments.CountAsync(c => c.UserId == userId && !c.IsDeleted),
                FeedbackToMe = await _db.StoryComments.CountAsync(c => !c.IsDeleted && c.Story.UserId == userId && c.UserId != userId)
            };

            var latestEntries = await _db.JournalEntries
                .Include(j => j.Emotion)
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAtUtc)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

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
                Moods = moods, // IEnumerable<Emotion>
                Stats = stats,
                LatestEntries = latestEntries,
                LatestMyComments = latestMyComments,
                LatestFeedback = latestFeedback,
                LatestMyStories = latestMyStories
            };

            return View(vm);
        }

        // Полный список записей (дневник)
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

        // GET /profile/entry/123 — тело модалки
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

        public class EntryEditVM
        {
            [Required] public int Id { get; set; }
            [Required, MinLength(1), DataType(DataType.MultilineText)]
            public string Content { get; set; } = null!;
        }

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

    // VM для модалки 
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