using ADPasswordManager.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ADPasswordManager.Services
{
    public class TokenCleanupService : IHostedService, IDisposable
    {
        private readonly ILogger<TokenCleanupService> _logger;
        private Timer _timer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public TokenCleanupService(
            ILogger<TokenCleanupService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Token Cleanup Service is starting.");

            // === PHẦN THAY ĐỔI ĐỂ DÙNG SỐ THẬP PHÂN ===

            // 1. Đọc giá trị kiểu "double" (thay vì "int"), mặc định là 6.0 giờ
            double intervalHours = _configuration.GetValue<double>("TaskSettings:CleanupIntervalHours", 6.0);

            // 2. Kiểm tra giá trị (phải lớn hơn 0)
            if (intervalHours <= 0)
            {
                intervalHours = 6.0; // Đặt lại giá trị mặc định nếu cấu hình sai
                _logger.LogWarning("CleanupIntervalHours invalid or not set in appsettings.json, defaulting to 6.0 hours.");
            }

            _logger.LogInformation($"Token Cleanup Service will run every {intervalHours} hours.");

            // 3. TimeSpan.FromHours() HỖ TRỢ SẴN kiểU "double"
            // Ví dụ: 0.5 giờ sẽ tự động trở thành 30 phút.
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(intervalHours));

            // === KẾT THÚC THAY ĐỔI ===

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            _logger.LogInformation("Token Cleanup Service is running.");

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                try
                {
                    var tokensToRemove = context.PasswordResetTokens
                        .Where(t => t.IsUsed == true || t.ExpiryTimestamp < DateTime.UtcNow)
                        .ToList();

                    if (tokensToRemove.Any())
                    {
                        context.PasswordResetTokens.RemoveRange(tokensToRemove);
                        context.SaveChanges();
                        _logger.LogInformation($"Token Cleanup: Removed {tokensToRemove.Count} expired/used tokens.");
                    }
                    else
                    {
                        _logger.LogInformation("Token Cleanup: No tokens to remove.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during token cleanup.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Token Cleanup Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}