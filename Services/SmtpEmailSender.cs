using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace FeelShare.Web.Services
{
    public class SmtpOptions
    {
        public string Host { get; set; } = default!;
        public int Port { get; set; } = 587;
        public string From { get; set; } = default!;
        public string User { get; set; } = default!;
        public string Password { get; set; } = default!;
        public bool EnableSsl { get; set; } = true;
    }

    public class SmtpEmailSender(IOptions<SmtpOptions> opt) : IEmailSender
    {
        private readonly SmtpOptions _o = opt.Value;

        public async Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody = null)
        {
            using var msg = new MailMessage(_o.From, to)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            if (!string.IsNullOrEmpty(plainTextBody))
                msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainTextBody, null!, "text/plain"));

            using var client = new SmtpClient(_o.Host, _o.Port)
            {
                Credentials = new NetworkCredential(_o.User, _o.Password),
                EnableSsl = _o.EnableSsl
            };
            await client.SendMailAsync(msg);
        }
    }
}