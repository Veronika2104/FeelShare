using System.ComponentModel.DataAnnotations;

namespace FeelShare.Web.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required] public string UserId { get; set; } = null!;
        [Required] public string Token { get; set; } = null!;

        [Required, DataType(DataType.Password), MinLength(8)]
        public string Password { get; set; } = null!;

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = null!;
    }
}