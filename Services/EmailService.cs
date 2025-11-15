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

            var secureSocketOptions = SecureSocketOptions.Auto; // Mặc định là Auto
            if (!string.IsNullOrEmpty(_smtpSettings.SecurityProtocol))
            {
                // Dùng switch để dễ dàng mở rộng
                switch (_smtpSettings.SecurityProtocol.ToLower())
                {
                    case "ssl":
                    case "ssl_tls":
                    case "sslonconnect":
                        secureSocketOptions = SecureSocketOptions.SslOnConnect;
                        break;
                    case "starttls":
                        secureSocketOptions = SecureSocketOptions.StartTls;
                        break;
                    case "starttlswhenavailable":
                        secureSocketOptions = SecureSocketOptions.StartTlsWhenAvailable;
                        break;
                    case "auto":
                    default:
                        secureSocketOptions = SecureSocketOptions.Auto;
                        break;
                }
            }

            //// Kết nối tới server SMTP
            //await smtp.ConnectAsync(_smtpSettings.SmtpServer, _smtpSettings.SmtpPort, SecureSocketOptions.StartTls);
            //// Kết nối tới server SMTP
            //await smtp.ConnectAsync(_smtpSettings.SmtpServer, _smtpSettings.SmtpPort, SecureSocketOptions.SslOnConnect);

            await smtp.ConnectAsync(_smtpSettings.SmtpServer, _smtpSettings.SmtpPort, secureSocketOptions);
            // Xác thực (nếu có user/pass)
            await smtp.AuthenticateAsync(_smtpSettings.SmtpUser, _smtpSettings.SmtpPass);

            // Gửi email
            await smtp.SendAsync(email);

            // Ngắt kết nối
            await smtp.DisconnectAsync(true);
        }
    }
}