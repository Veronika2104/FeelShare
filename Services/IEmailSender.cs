namespace FeelShare.Web.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody = null);
    }
}
