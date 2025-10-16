using FeelShare.Web.Models;
using System.Collections.Generic;

namespace FeelShare.Web.ViewModels
{
    public class CreateStoryVM
    {
        public IEnumerable<Emotion> Moods { get; set; } = Enumerable.Empty<Emotion>();
        public int? SelectedEmotionId { get; set; }
        public string? ReturnUrl { get; set; }
    }
}