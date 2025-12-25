using FeelShare.Web.Models;
using System.Collections.Generic;

namespace FeelShare.Web.ViewModels
{
    public class CreateStoryVM
    {
        public List<Emotion> Moods { get; set; } = new();

        // Предвыбранная эмоция (если пришли с фильтра по эмоции)
        public int? SelectedEmotionId { get; set; }

        // Куда вернуться после публикации 
        public string? ReturnUrl { get; set; }
    }
}