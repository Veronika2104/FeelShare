using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.ViewModels
{
    public class RegisterViewModel
    {
        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = null!;

        [Display(Name = "Имя (показывается другим)")]
        public string? DisplayName { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "Пароль")]
        public string Password { get; set; } = null!;

        [Required, DataType(DataType.Password), Display(Name = "Подтвердите пароль"),
         Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; } = null!;
    }
}