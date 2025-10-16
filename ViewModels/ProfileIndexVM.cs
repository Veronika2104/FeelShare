using FeelShare.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeelShare.Web.ViewModels
{
    public class ProfileStatsVM
    {
        public int MyEntries { get; set; }      // записей в дневнике
        public int MyStories { get; set; }      // анонимных историй
        public int MyComments { get; set; }     // моих комментариев
        public int FeedbackToMe { get; set; }   // комментариев к моим историям
    }

    public class MyCommentRowVM
    {
        public int StoryId { get; set; }
        public int CommentId { get; set; }
        public string StoryPreview { get; set; } = "";
        public string CommentPreview { get; set; } = "";
        public DateTime CreatedAtLocal { get; set; }
    }

    public class FeedbackRowVM
    {
        public int StoryId { get; set; }
        public int CommentId { get; set; }
        public string From { get; set; } = "";       // кто написал
        public string CommentPreview { get; set; } = "";
        public DateTime CreatedAtLocal { get; set; }
    }

    //карточка моей истории для профиля
    public class MyStoryRowVM
    {
        public int Id { get; set; }
        public string EmotionName { get; set; } = "";
        public string EmotionIcon { get; set; } = "";
        public string Preview { get; set; } = "";
        public DateTime CreatedAtLocal { get; set; }
        public int CommentsCount { get; set; }
        public int ReactionsCount { get; set; }
    }

    public class ProfileIndexVM
    {
        public string DisplayName { get; set; } = "";
        public IEnumerable<Emotion> Moods { get; set; } = Enumerable.Empty<Emotion>();
        public int? SelectedEmotionId { get; set; }

        public ProfileStatsVM Stats { get; set; } = new ProfileStatsVM();

        // последние активности для превью
        public List<JournalEntry> LatestEntries { get; set; } = new();            // с Emotion включённым
        public List<MyCommentRowVM> LatestMyComments { get; set; } = new();
        public List<FeedbackRowVM> LatestFeedback { get; set; } = new();

        public List<MyStoryRowVM> LatestMyStories { get; set; } = new();
    }
}