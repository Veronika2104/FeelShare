using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.Models
{
    public class MoodAdvice
    {
        public int Id { get; set; }

        // Категория по среднему настроению
        [Required, MaxLength(10)]
        public string Category { get; set; } = "neutral";
        // Текст совета
        [Required]
        public string Text { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
