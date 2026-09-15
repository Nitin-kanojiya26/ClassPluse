using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassPluse.Services
{
    public class MockEmailService : IEmailService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(IWebHostEnvironment env, ILogger<MockEmailService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                // Create Emails directory if it doesn't exist
                string emailsFolder = Path.Combine(_env.ContentRootPath, "Emails");
                if (!Directory.Exists(emailsFolder))
                {
                    Directory.CreateDirectory(emailsFolder);
                }

                // Generate file name
                string fileName = $"Email_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.txt";
                string filePath = Path.Combine(emailsFolder, fileName);

                // Write email content
                string emailContent = $"--- EMAIL MESSAGE ---\nTo: {to}\nSubject: {subject}\nDate: {DateTime.Now}\n\nBody:\n{body}\n---------------------\n";
                await File.WriteAllTextAsync(filePath, emailContent);
                
                _logger.LogInformation($"Mock email saved to {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write mock email to disk.");
            }
        }
    }
}
