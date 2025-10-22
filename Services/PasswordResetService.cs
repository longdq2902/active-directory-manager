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

                // 5. Soạn và gửi email
                string subject = "Active Directory Password Reset Request"; 
                string body = $@"
                    <p>Hello,</p>
                    <p>We received a request to reset the password for your account <strong>{username}</strong>.</p>
                    <p>Please click the link below to set a new password. This link will expire in {tokenLifetimeMinutes} minutes.</p>
                    <p><a href='{resetLink}'><strong>RESET YOUR PASSWORD</strong></a></p>
                    <p>If you did not request this, please ignore this email.</p>
                    <p>Regards,<br>AD Management System</p>"; 

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