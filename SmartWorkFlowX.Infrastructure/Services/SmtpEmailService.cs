using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Infrastructure.Services
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

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _config["Email:SenderName"] ?? "SmartWorkFlowX",
                    _config["Email:SenderEmail"] ?? "noreply@smartworkflowx.com"
                ));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                
                var host = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var port = int.TryParse(_config["Email:SmtpPort"], out int p) ? p : 587;
                var password = _config["Email:Password"] ?? "";
                
                // For local dev without real creds, just log it
                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning($"[EMAIL MOCK] To: {toEmail} | Subject: {subject}");
                    _logger.LogWarning($"[EMAIL MOCK] Body: {htmlBody}");
                    return;
                }

                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_config["Email:SenderEmail"], password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                throw;
            }
        }
    }
}
