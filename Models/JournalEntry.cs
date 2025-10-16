using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeelShare.Web.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public int EmotionId { get; set; }
        public Emotion Emotion { get; set; } = null!;

        // Храним как NVARCHAR(MAX) — хватит и на 5000+ слов
        [Required, Column(TypeName = "nvarchar(max)")]
        public string Content { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}