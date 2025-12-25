using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeelShare.Web.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }

        // Владелец записи
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        // Эмоция, с которой связана запись
        public int EmotionId { get; set; }
        public Emotion Emotion { get; set; } = null!;

        //  текст дневника
        public string Content { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}