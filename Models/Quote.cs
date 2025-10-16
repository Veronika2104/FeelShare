using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation; // ← добавь

namespace FeelShare.Web.Models
{
    public class Quote
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Выберите эмоцию")]
        public int EmotionId { get; set; }

        [ValidateNever]                 // не валидировать навигацию
        public Emotion? Emotion { get; set; }  

        [Required, StringLength(1024, ErrorMessage = "Макс. 1024 символа")]
        public string Text { get; set; } = null!;

        [StringLength(128, ErrorMessage = "Макс. 128 символов")]
        public string? Author { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
    }
}