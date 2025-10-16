using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
    }
}