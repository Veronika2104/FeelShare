using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeelShare.Web.Models
{
    public class PublicStory
    {
        public int Id { get; set; }

        [Required]
        public int EmotionId { get; set; }//ссылка на эмоцию
        public Emotion Emotion { get; set; } = null!;

        // Пишем, кто создал (для модерации), но не показываем имя
        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        [Required, Column(TypeName = "nvarchar(max)")]
        public string Content { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        //  (модерация/скрытие)
        public bool IsPublished { get; set; } = true;

        public ICollection<StoryLike> Likes { get; set; } = new List<StoryLike>();
      
        public ICollection<StoryReaction> Reactions { get; set; } = new List<StoryReaction>();
        public ICollection<StoryComment> Comments { get; set; } = new List<StoryComment>();//лайки, реакции ,комментарии.
    }

    public class StoryLike
    {
        public int Id { get; set; }

        [Required]
        public int StoryId { get; set; }
       // указывает, к какой истории поставили лайк.
        public PublicStory Story { get; set; } = null!;

        [Required, MaxLength(100)]
        public string LikeKey { get; set; } = null!;//уникальный ключ, чтобы один человек не мог накрутить лайки.

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;//Когда лайк был поставлен.
    }
}