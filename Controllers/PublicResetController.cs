using ADPasswordManager.Data;
using ADPasswordManager.Models.ViewModels;
using ADPasswordManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using ADPasswordManager.Models.Entities; // <-- Cần thêm
using System.Linq; // <-- Cần thêm

namespace ADPasswordManager.Controllers
{
    public class PublicResetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ADManagementService _adService;
        private readonly ILogger<PublicResetController> _logger;

        public PublicResetController(
            ApplicationDbContext context,
            ADManagementService adService,
            ILogger<PublicResetController> logger)
        {
            _context = context;
            _adService = adService;
            _logger = logger;
        }

        // Bước 1: Hiển thị form reset
        [HttpGet]
        public async Task<IActionResult> Reset(string token)
        {
            var tokenRecord = await ValidateToken(token);
            if (tokenRecord == null)
            {
                // Token không hợp lệ, trả về view lỗi
                var errorModel = new PublicResetResultViewModel
                {
                    Title = "Invalid Link", // <-- THAY ĐỔI
                    Message = "This password reset link is invalid, has expired, or has already been used. Please ask your administrator to send a new link.", // <-- THAY ĐỔI
                    IsSuccess = false
                };
                return View("Result", errorModel);
            }

            // Token hợp lệ, hiển thị form reset
            var model = new PublicResetViewModel
            {
                Token = token,
                Username = tokenRecord.Username
            };

            return View(model);
        }

        // Bước 2: Xử lý việc reset mật khẩu
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Reset(PublicResetViewModel model);
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset(PublicResetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model); // Trả về form nếu validation lỗi (ví dụ: pass không khớp)
            }

            var tokenRecord = await ValidateToken(model.Token);
            if (tokenRecord == null)
            {
                var errorModel = new PublicResetResultViewModel
                {
                    Title = "Invalid Link", // <-- THAY ĐỔI
                    Message = "This password reset link is invalid, has expired, or has already been used.", 
                    IsSuccess = false
                };
                return View("Result", errorModel);
            }

            try
            {
                // 1. Reset mật khẩu trong AD
                // (Chúng ta cần một phương thức mới trong ADManagementService cho việc này)
                await _adService.ResetPasswordPublicAsync(model.Username, model.NewPassword);

                // 2. Đánh dấu token đã sử dụng
                tokenRecord.IsUsed = true;
                _context.PasswordResetTokens.Update(tokenRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User '{model.Username}' successfully reset their password via public link."); // (Optional change)

                // 3. Trả về view thành công
                var successModel = new PublicResetResultViewModel
                {
                    Title = "Success!", // <-- THAY ĐỔI
                    Message = "Your password has been successfully reset. You may now close this window and log in with your new password.",
                    IsSuccess = true
                };
                return View("Result", successModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while '{model.Username}' was self-resetting password.");
                // Lỗi từ AD (ví dụ: mật khẩu không đủ phức tạp)
                ModelState.AddModelError(string.Empty, "Password reset failed. " + ex.Message); 
                return View(model);
            }
        }

        // Hàm helper để kiểm tra token
        private async Task<PasswordResetToken> ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenRecord = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token);

            // Kiểm tra 3 điều kiện
            if (tokenRecord == null || tokenRecord.IsUsed || tokenRecord.ExpiryTimestamp < DateTime.UtcNow)
            {
                return null; // Không tìm thấy, hoặc đã dùng, hoặc đã hết hạn
            }

            return tokenRecord;
        }
    }
}