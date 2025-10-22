using ADPasswordManager.Models.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
//using System.Net.Mail;

namespace ADPasswordManager.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(IOptions<SmtpSettings> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();
            email.Sender = new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.SenderEmail);
            email.From.Add(new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder();
            builder.HtmlBody = body; // Chúng ta sẽ gửi email dạng HTML
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();

            // Kết nối tới server SMTP
            await smtp.ConnectAsync(_smtpSettings.SmtpServer, _smtpSettings.SmtpPort, SecureSocketOptions.StartTls);

            // Xác thực (nếu có user/pass)
            await smtp.AuthenticateAsync(_smtpSettings.SmtpUser, _smtpSettings.SmtpPass);

            // Gửi email
            await smtp.SendAsync(email);

            // Ngắt kết nối
            await smtp.DisconnectAsync(true);
        }
    }
}