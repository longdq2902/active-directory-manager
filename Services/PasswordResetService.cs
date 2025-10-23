using ADPasswordManager.Data;
using ADPasswordManager.Models.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ADPasswordManager.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordResetService> _logger;

        public PasswordResetService(
            ApplicationDbContext context,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<PasswordResetService> logger)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> GenerateAndSendResetLinkAsync(string username, string userEmail)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                _logger.LogWarning($"Nỗ lực gửi link reset cho '{username}' thất bại: không có email.");
                throw new Exception("Người dùng này không có địa chỉ email trong AD.");
            }

            try
            {
                // 1. Tạo token
                string token = Guid.NewGuid().ToString("N"); // "N" = không có dấu gạch ngang

                // 2. Lấy public URL từ appsettings.json
                string publicUrl = _configuration["PublicFacingUrl"];
                if (string.IsNullOrWhiteSpace(publicUrl))
                {
                    _logger.LogError("PublicFacingUrl chưa được cấu hình trong appsettings.json.");
                    throw new Exception("Lỗi cấu hình hệ thống.");
                }

                string resetLink = $"{publicUrl.TrimEnd('/')}/PublicReset/Reset?token={token}";

                // 3. Tạo bản ghi token
                int tokenLifetimeMinutes = _configuration.GetValue<int>("TaskSettings:TokenLifetimeMinutes", 15);
                var tokenRecord = new PasswordResetToken
                {
                    Username = username,
                    Token = token,
                    ExpiryTimestamp = DateTime.UtcNow.AddMinutes(tokenLifetimeMinutes),
                    IsUsed = false
                };

                // 4. Lưu token vào DB
                _context.PasswordResetTokens.Add(tokenRecord);
                await _context.SaveChangesAsync();

                // 5. Read email template from config and send email
                string subjectTemplate = _configuration["EmailTemplates:PasswordReset:Subject"];
                string bodyTemplate = _configuration["EmailTemplates:PasswordReset:BodyHtml"];
                //int tokenLifetimeMinutes = _configuration.GetValue<int>("TaskSettings:TokenLifetimeMinutes", 15); // Read lifetime again

                if (string.IsNullOrWhiteSpace(subjectTemplate) || string.IsNullOrWhiteSpace(bodyTemplate))
                {
                    _logger.LogError("Email subject or body template is missing in appsettings.json.");
                    throw new Exception("Email template configuration error.");
                }

                // Replace placeholders
                string subject = subjectTemplate; // Subject might not need placeholders, but kept for consistency
                string body = bodyTemplate
                                .Replace("{username}", username)
                                .Replace("{resetLink}", resetLink)
                                .Replace("{tokenLifetimeMinutes}", tokenLifetimeMinutes.ToString());


                await _emailService.SendEmailAsync(userEmail, subject, body);

                _logger.LogInformation($"Đã gửi link reset thành công cho '{username}' tới email '{userEmail}'.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi tạo và gửi link reset cho '{username}'.");
                return false;
            }
        }
    }
}