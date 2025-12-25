using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.Models
{
    public class StoryReport
    {
        public int Id { get; set; }

        [Required]
        public int StoryId { get; set; }
        public PublicStory Story { get; set; } = null!;

       
        [Required, MaxLength(100)]
        public string ReportKey { get; set; } = null!;

        [MaxLength(40)]
        public string? Reason { get; set; } 

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}