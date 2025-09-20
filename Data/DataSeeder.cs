using ADPasswordManager.Data;
using ADPasswordManager.Models.Configuration;
using ADPasswordManager.Models.Entities;
using Microsoft.EntityFrameworkCore;

public static class DataSeeder
{
    public static void Seed(IApplicationBuilder app)
    {
        using (var serviceScope = app.ApplicationServices.CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();
            var configuration = serviceScope.ServiceProvider.GetService<IConfiguration>();

            if (context == null || configuration == null)
            {
                return;
            }

            // Đảm bảo database đã được tạo
            context.Database.Migrate();

            // Kiểm tra xem đã có dữ liệu trong bảng DelegationRules chưa
            if (!context.DelegationRules.Any())
            {
                // Lấy dữ liệu từ appsettings.json
                var delegationSettings = configuration.GetSection("DelegationSettings").Get<DelegationSettings>();

                if (delegationSettings != null && delegationSettings.AdminMappings.Any())
                {
                    var rulesToAdd = new List<DelegationRule>();
                    foreach (var mapping in delegationSettings.AdminMappings)
                    {
                        rulesToAdd.Add(new DelegationRule
                        {
                            AdminGroup = mapping.AdminGroup,
                            // Chuyển danh sách ManagedGroups thành một chuỗi duy nhất, phân tách bằng dấu phẩy
                            ManagedGroups = string.Join(",", mapping.ManagedGroups)
                        });
                    }

                    context.DelegationRules.AddRange(rulesToAdd);
                    context.SaveChanges();
                }
            }
        }
    }
}