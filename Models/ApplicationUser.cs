using Microsoft.AspNetCore.Identity;

namespace FeelShare.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
      
        public string? DisplayName { get; set; }
        public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    }
}
