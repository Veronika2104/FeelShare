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
    // Контроллер отвечает за авторизацию/регистрацию и восстановление доступа
    public class AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender) : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
        private readonly IEmailSender _emailSender = emailSender;

        //Регистрация 
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            // returnUrl нужен, чтобы после входа/регистрации вернуть пользователя туда, где он был
            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Если в модели ошибки  возвращаю форму
            if (!ModelState.IsValid) return View(vm);

            // Создаю пользователя на основе введённых данных
            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                DisplayName = string.IsNullOrWhiteSpace(vm.DisplayName) ? null : vm.DisplayName
            };

            // Пытаюсь создать аккаунт в Identity
            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                // Ошибки Identity добавляю в ModelState
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            // Подтверждение email 
            // Генерирую токен подтверждения и кодирую его, чтобы можно было безопасно вставить в URL
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Делаю абсолютную ссылку на ConfirmEmail
            var confirmUrl = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, token = tokenEncoded, returnUrl },
                Request.Scheme
            )!;

            try
            {
                // Отправляю письмо подтверждения
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
                // Если письмо не отправилось — регистрацию мы уже сделали, просто предупреждаем
                TempData["Error"] = "Не удалось отправить письмо подтверждения: " + ex.Message;
            }

            // Перевожу на страницу ожидаем подтверждение
            return RedirectToAction(nameof(RegistrationPending));
        }

        [HttpGet]
        public IActionResult RegistrationPending() => View();

        // Вход 
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

            // Пытаюсь залогинить по email/паролю
            var result = await _signInManager.PasswordSignInAsync(
                vm.Email,
                vm.Password,
                vm.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // Возвращаю на returnUrl или на Profile/Me по умолчанию
                return RedirectToLocal(
                    vm.ReturnUrl,
                    fallbackAction: nameof(ProfileController.Me),
                    fallbackController: "Profile");
            }

            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(vm);
        }

        //  Выход 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // Страница доступ запрещён
        [HttpGet]
        public IActionResult Denied() => View();

      
        private IActionResult RedirectToLocal(string? returnUrl, string fallbackAction, string fallbackController)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(fallbackAction, fallbackController);
        }

        //  Подтверждение email 
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token, string? returnUrl = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return View("ConfirmEmail", false);

            // Декодирую токен из URL обратно в исходную строку
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

            // Подтверждаю email через Identity
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (result.Succeeded)
            {
                // После подтверждения можно сразу залогинить пользователя
                await _signInManager.SignInAsync(user, isPersistent: false);

                ViewBag.ReturnUrl = returnUrl;
                return View("ConfirmEmail", true);
            }

            return View("ConfirmEmail", false);
        }

        // ===== Повторная отправка письма подтверждения =====
        [HttpGet]
        public IActionResult ResendConfirmation() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendConfirmation(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Не раскрываем, существует ли пользователь — даём одинаковое сообщение
            if (user == null || user.EmailConfirmed)
            {
                TempData["Success"] = "Если такой пользователь существует, мы отправили письмо ещё раз.";
                return RedirectToAction(nameof(Login));
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var confirmUrl = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, token = tokenEncoded },
                Request.Scheme
            )!;

            await _emailSender.SendAsync(
                user.Email!,
                "FeelShare — подтвердите email",
                $"<p>Подтвердите email: <a href=\"{confirmUrl}\">ссылка</a></p>"
            );

            TempData["Success"] = "Мы отправили письмо с новой ссылкой.";
            return RedirectToAction(nameof(Login));
        }

        // ===== Восстановление пароля =====
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

            // Отправляем письмо только если пользователь есть и email подтверждён
            if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var tokenEnc = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                var resetUrl = Url.Action(
                    nameof(ResetPassword),
                    "Account",
                    new { userId = user.Id, token = tokenEnc },
                    Request.Scheme
                )!;

                try
                {
                    await _emailSender.SendAsync(
                        user.Email!,
                        "FeelShare — сброс пароля",
                        $"<p>Чтобы сбросить пароль, перейдите по ссылке: <a href=\"{resetUrl}\">сбросить пароль</a></p>"
                    );
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Не удалось отправить письмо: " + ex.Message;
                }
            }

            // Важно: не раскрываем, существует ли пользователь
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View();

        // GET форма сброса пароля (переход по ссылке из письма)
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return BadRequest();

            return View(new ResetPasswordViewModel { UserId = userId, Token = token });
        }

        // POST сброс пароля
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByIdAsync(vm.UserId);

            // Если пользователя нет — не палим информацию, просто показываем подтверждение
            if (user == null) return RedirectToAction(nameof(ResetPasswordConfirmation));

            // Декодирую токен обратно из URL-safe формата
            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(vm.Token));
            }
            catch
            {
                return BadRequest("Invalid token");
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, vm.Password);

            if (result.Succeeded)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            // Ошибки Identity 
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);

            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation() => View();
    }
}
