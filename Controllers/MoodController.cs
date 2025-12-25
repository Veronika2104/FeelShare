using System;
using System.Linq;
using System.Threading.Tasks;
using FeelShare.Web.Data;
using FeelShare.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeelShare.Web.Controllers
{
    [Authorize] // сюда только после входа 
    public class MoodController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _um;

        public MoodController(AppDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _um = um;
        }

        // есть ли замер за последние 24 часа (чтобы показывать пора заполнить)
        [HttpGet("/mood/check")]
        public async Task<IActionResult> Check()
        {
            var uid = _um.GetUserId(User);
            var since = DateTime.UtcNow.AddHours(-24);

            var hasRecent = await _db.MoodSurveys
                .AsNoTracking()
                .AnyAsync(s => s.UserId == uid && s.CreatedAtUtc >= since);

            // need=true означает: нужно показать опрос, потому что недавно не заполняли
            return Json(new { need = !hasRecent });
        }

        
        [HttpPost("/mood/save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromForm] Dictionary<int, int> scores)
        {
       
            if (scores == null || scores.Count == 0)
                return BadRequest("Данные невалидны.");

            var uid = _um.GetUserId(User);

            var survey = new MoodSurvey
            {
                UserId = uid!,
                CreatedAtUtc = DateTime.UtcNow
            };

            // собираю items опроса по каждой эмоции свой балл
            foreach (var kv in scores)
            {
                var emotionId = kv.Key;
                var sc = Math.Max(0, Math.Min(5, kv.Value)); 

                survey.Items.Add(new MoodSurveyItem
                {
                    EmotionId = emotionId,
                    Score = sc
                });
            }

            _db.MoodSurveys.Add(survey);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Настроение сохранено. Спасибо! 💜";
            return Redirect(Request.Headers["Referer"].ToString() ?? "/");
        }
    }
}