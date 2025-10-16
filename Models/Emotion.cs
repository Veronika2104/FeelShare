using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.Models
{
    public class Emotion
    {
        public int Id { get; set; }                // PK в БД (Identity)

        [Required, MaxLength(64)]
        public string Slug { get; set; } = null!;  // человеческий адрес: /emotion/{slug}

        [Required, MaxLength(64)]
        public string Name { get; set; } = null!;  // Заголовок на карточке (“Радость”)

        [MaxLength(8)]
        public string Icon { get; set; } = "💬";   // Эмодзи/символ. Хранится как текст.

        public ICollection<Quote> Quotes { get; set; } = new List<Quote>(); // навигация 1..* к цитатам
    }
}