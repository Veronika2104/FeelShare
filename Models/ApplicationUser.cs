using Microsoft.AspNetCore.Identity;

namespace FeelShare.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Имя, которое пользователь видит на сайте
        public string? DisplayName { get; set; }
        // Дата регистрации
        public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    }
}
