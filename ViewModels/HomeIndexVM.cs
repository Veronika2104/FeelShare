using FeelShare.Web.Models;

namespace FeelShare.Web.ViewModels
{
    public class HomeIndexVM
    {
        public List<Emotion> Moods { get; set; } = new();
        public List<StoryListItemVM> Stories { get; set; } = new();
        public int? SelectedEmotionId { get; set; }
        public int Page { get; set; }
        public bool HasMore { get; set; }

        // Пагинация курсором

        public string? NextAfterCreated { get; set; }  // ISO 8601 (UTC)
        public int? NextAfterId { get; set; }          // int, т.к. Id у PublicStory — int

        // Фильтр по периодам
        // null | "today" | "7d" | "30d" | "week"
        public string? Period { get; set; }
        // Смещение недель для Period=="week": 0 = эта, 1 = прошлая и т.д.
        public int? WeekOffset { get; set; }
    }


    public class StoryListItemVM
    {
        public int Id { get; set; }
        public int EmotionId { get; set; }
        public string EmotionName { get; set; } = "";
        public string EmotionIcon { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
        public int LikesCount { get; set; }
        public bool IsLiked { get; set; }   // <— добавили
                                            // реакции
        public Dictionary<string, int> ReactionCounts { get; set; } = new();
        public List<string> MyReactions { get; set; } = new();

        // комментарии
        public int CommentsCount { get; set; }
        public List<CommentItemVM> LatestComments { get; set; } = new();
    }

    public class CommentItemVM
    {
        public int Id { get; set; }
        public string Author { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
        public bool IsOwner { get; set; }

    }

}