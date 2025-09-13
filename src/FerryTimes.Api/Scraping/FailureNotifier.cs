using System.Net;
using System.Net.Mail;

namespace FerryTimes.Api.Scraping
{
    public class FailureNotifier
    {
        private readonly IConfiguration _configuration;

        public FailureNotifier(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task NotifyFailureAsync(string scraperName, string errorMessage)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var smtpHost = smtpSettings["Host"] ?? throw new InvalidOperationException("SMTP Host is not configured.");
            var smtpPort = smtpSettings["Port"] ?? throw new InvalidOperationException("SMTP Port is not configured.");
            var smtpUsername = smtpSettings["Username"] ?? throw new InvalidOperationException("SMTP Username is not configured.");
            var smtpPassword = smtpSettings["Password"] ?? throw new InvalidOperationException("SMTP Password is not configured.");
            var smtpEnableSsl = smtpSettings["EnableSsl"] ?? throw new InvalidOperationException("SMTP EnableSsl is not configured.");
            var fromEmail = smtpSettings["FromEmail"] ?? throw new InvalidOperationException("From Email is not configured.");
            var toEmail = smtpSettings["ToEmail"] ?? throw new InvalidOperationException("To Email is not configured.");

            var smtpClient = new SmtpClient(smtpHost)
            {
                Port = int.Parse(smtpPort),
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = bool.Parse(smtpEnableSsl),
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = $"[Scraper Alert] {scraperName} failed",
                Body = $"The scraper '{scraperName}' encountered an error:\n\n{errorMessage}\n\nTime: {DateTime.UtcNow}",
                IsBodyHtml = false,
            };
            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
