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

                // Add headers to improve deliverability and prevent spam filtering
                message.Headers.Add("X-Priority", "3");
                message.Headers.Add("Importance", "Normal");
                message.Headers.Add("X-Mailer", "SmartWorkFlowX");
                message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
                
                var bodyBuilder = new BodyBuilder 
                { 
                    HtmlBody = htmlBody,
                    TextBody = StripHtmlTags(htmlBody) // Add plain text alternative
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                
                var host = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var port = int.TryParse(_config["Email:SmtpPort"], out int p) ? p : 587;
                var password = _config["Email:Password"] ?? "";
                var senderEmail = _config["Email:SenderEmail"];
                
                // For local dev without real creds, just log it
                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning($"[EMAIL MOCK] To: {toEmail} | Subject: {subject}");
                    _logger.LogWarning($"[EMAIL MOCK] Body: {htmlBody}");
                    return;
                }

                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                
                _logger.LogInformation($"Email sent successfully to {toEmail} with subject '{subject}'");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                throw;
            }
        }

        private string StripHtmlTags(string html)
        {
            var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "");
            plainText = System.Net.WebUtility.HtmlDecode(plainText);
            return plainText;
        }
    }
}
