using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClassPluse.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var smtpSettings = _config.GetSection("SmtpSettings");
                var server = smtpSettings["Server"];
                var portString = smtpSettings["Port"];
                var port = string.IsNullOrEmpty(portString) ? 587 : int.Parse(portString);
                var senderEmail = smtpSettings["SenderEmail"];
                var senderName = smtpSettings["SenderName"];
                var password = smtpSettings["Password"];

                using var client = new SmtpClient(server, port)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail ?? "no-reply@classpluse.com", senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email successfully sent to {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                throw; // Important: we want to know if it fails
            }
        }
    }
}
