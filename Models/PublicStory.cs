using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeelShare.Web.Models
{
    public class PublicStory
    {
        public int Id { get; set; }

        [Required]
        public int EmotionId { get; set; }
        public Emotion Emotion { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        [Required ]
        public string Content { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        
        public bool IsPublished { get; set; } = true;

        //статус модерации
        public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.Published;

        // сколько жалоб
        public int ReportsCount { get; set; } = 0;

        //  причина (для админки)
        [MaxLength(300)]
        public string? ModerationNote { get; set; }

  
        public DateTime? ModeratedAtUtc { get; set; }
        [MaxLength(450)]
        public string? ModeratedByUserId { get; set; }

        public ICollection<StoryLike> Likes { get; set; } = new List<StoryLike>();
        public ICollection<StoryReaction> Reactions { get; set; } = new List<StoryReaction>();
        public ICollection<StoryComment> Comments { get; set; } = new List<StoryComment>();

        
        public ICollection<StoryReport> Reports { get; set; } = new List<StoryReport>();
    }

    public class StoryLike
    {
        public int Id { get; set; }

        [Required]
        public int StoryId { get; set; }
        public PublicStory Story { get; set; } = null!;

        [Required, MaxLength(100)]
        public string LikeKey { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}