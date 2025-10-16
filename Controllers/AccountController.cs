using FeelShare.Web.Models;
using FeelShare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using FeelShare.Web.Services;
using Microsoft.SqlServer.Server;

namespace FeelShare.Web.Controllers
{
    public class AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender) : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
        private readonly IEmailSender _emailSender = emailSender;
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(vm);

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                DisplayName = string.IsNullOrWhiteSpace(vm.DisplayName) ? null : vm.DisplayName
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View(vm);
            }

            // Генерация токена и ссылки подтверждения
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            //  кодируем, чтобы поместился в URL
            var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // собираем абсолютную ссылку на наш ConfirmEmail
            var confirmUrl = Url.Action(nameof(ConfirmEmail), "Account",
                new { userId = user.Id, token = tokenEncoded, returnUrl }, Request.Scheme)!;

            try
            {   //  шлём письмо
                await _emailSender.SendAsync(
                    to: user.Email!,
                    subject: "FeelShare — подтвердите email",
                    htmlBody: $"""
                <p>Здравствуйте!</p>
                <p>Пожалуйста, подтвердите ваш email для завершения регистрации в <b>FeelShare</b>.</p>
                <p><a href="{confirmUrl}">Подтвердить email</a></p>
                <p>Если вы не регистрировались — просто игнорируйте это письмо.</p>
            """);
            }
            catch (Exception ex)
            {
             
                TempData["Error"] = "Не удалось отправить письмо подтверждения: " + ex.Message;
              
            }

            return RedirectToAction(nameof(RegistrationPending));
        }
        [HttpGet]
        public IActionResult RegistrationPending() => View();

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var result = await _signInManager.PasswordSignInAsync(
                vm.Email, vm.Password, vm.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return RedirectToLocal(vm.ReturnUrl, fallbackAction: nameof(ProfileController.Me), fallbackController: "Profile");

            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Denied() => View();

        private IActionResult RedirectToLocal(string? returnUrl, string fallbackAction, string fallbackController)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(fallbackAction, fallbackController);
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token, string? returnUrl = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return View("ConfirmEmail", false);

            string decodedToken;
            try
            {
                var tokenBytes = WebEncoders.Base64UrlDecode(token);
                decodedToken = Encoding.UTF8.GetString(tokenBytes);
            }
            catch
            {
                return View("ConfirmEmail", false);
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (result.Succeeded)
            {
                // Можно автоматически залогинить:
                await _signInManager.SignInAsync(user, isPersistent: false);
                ViewBag.ReturnUrl = returnUrl;
                return View("ConfirmEmail", true);
            }
            return View("ConfirmEmail", false);
        }
        [HttpGet]
        public IActionResult ResendConfirmation() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendConfirmation(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.EmailConfirmed)
            {
                TempData["Success"] = "Если такой пользователь существует, мы отправили письмо ещё раз.";
                return RedirectToAction(nameof(Login));
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmUrl = Url.Action(nameof(ConfirmEmail), "Account",
                new { userId = user.Id, token = tokenEncoded }, Request.Scheme)!;

            await _emailSender.SendAsync(user.Email!, "FeelShare — подтвердите email",
                $"<p>Подтвердите email: <a href=\"{confirmUrl}\">ссылка</a></p>");

            TempData["Success"] = "Мы отправили письмо с новой ссылкой.";
            return RedirectToAction(nameof(Login));
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);

            if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var tokenEnc = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var resetUrl = Url.Action(nameof(ResetPassword), "Account",
                    new { userId = user.Id, token = tokenEnc }, Request.Scheme)!;

                try
                {
                    await _emailSender.SendAsync(
                        user.Email!,
                        "FeelShare — сброс пароля",
                        $"<p>Чтобы сбросить пароль, перейдите по ссылке: <a href=\"{resetUrl}\">сбросить пароль</a></p>");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Не удалось отправить письмо: " + ex.Message;
                  
                }
            }

            // Не раскрываем, есть ли такой пользователь — всегда показываем подтверждение
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

                [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return BadRequest();
            return View(new ResetPasswordViewModel { UserId = userId, Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return RedirectToAction(nameof(ResetPasswordConfirmation));

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(vm.Token));
            }
            catch { return BadRequest("Invalid token"); }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, vm.Password);
            if (result.Succeeded) return RedirectToAction(nameof(ResetPasswordConfirmation));

            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation() => View();
    }
}