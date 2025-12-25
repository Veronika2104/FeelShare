using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.Models
{
    // Заголовок опроса 
    public class MoodSurvey
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = default!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<MoodSurveyItem> Items { get; set; } = new();
    }

    // Оценка по конкретной эмоции 
    public class MoodSurveyItem
    {
        public int Id { get; set; }

        public int SurveyId { get; set; }
        public MoodSurvey Survey { get; set; } = default!;

        public int EmotionId { get; set; }
        public Emotion Emotion { get; set; } = default!;

     
        [Range(0, 5)]
        public int Score { get; set; }
    }
}
