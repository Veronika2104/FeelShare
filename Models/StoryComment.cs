using FeelShare.Web.Models;
using System.ComponentModel.DataAnnotations;

public class StoryComment
{
    public int Id { get; set; }
    [Required] public int StoryId { get; set; }
    public PublicStory Story { get; set; } = null!;
    [Required] public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(4000)] public string Content { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}