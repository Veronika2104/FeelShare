using System;
using System.ComponentModel.DataAnnotations;


namespace YourNamespace.Models
{
    public enum MoodTag { None = 0, Night = 1, Anxiety = 2, Calmer = 3 }


    public class EmotionEntry
    {
        public int Id { get; set; }


        [Display(Name = "Создано")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        [Required, StringLength(120)]
        [Display(Name = "Заголовок")]
        public string Title { get; set; } = string.Empty;


        [Required]
        [Display(Name = "Текст")]
        public string Body { get; set; } = string.Empty;


        [Display(Name = "Тег настроения")]
        public MoodTag Tag { get; set; } = MoodTag.None;


        [Range(1, 5)]
        [Display(Name = "Интенсивность (1–5)")]
        public int Intensity { get; set; } = 3; 


        [Display(Name = "Безопасный ночной режим по умолчанию")]
        public bool IsNightSafe { get; set; } = false;
    }
}