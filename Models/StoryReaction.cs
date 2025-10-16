using FeelShare.Web.Models;
using System.ComponentModel.DataAnnotations;

public class StoryReaction
{
    public int Id { get; set; }
    [Required] public int StoryId { get; set; }
    public PublicStory Story { get; set; } = null!;
    [Required, MaxLength(16)] public string Reaction { get; set; } = null!;
    [Required, MaxLength(100)] public string ReactKey { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}