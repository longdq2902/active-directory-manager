using ADPasswordManager.Data;
using ADPasswordManager.Models.Configuration;
using ADPasswordManager.Services;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Cấu hình logger của Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs/ad-password-manager-.txt"), rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();




Log.Information("Starting up the application");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Bảo với host sử dụng Serilog thay vì logger mặc định
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(@"C:\logs\ad-password-manager-.txt", rollingInterval: RollingInterval.Day)); // <-- Thêm dòng này
        

    // Add services to the container.
    // Lấy ra DbContext connection string
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    // Cấu hình DbContext
    builder.Services.AddDbContext<ADPasswordManager.Data.ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();
    // Cấu hình Identity
    builder.Services.AddDefaultIdentity<Microsoft.AspNetCore.Identity.IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
        .AddEntityFrameworkStores<ADPasswordManager.Data.ApplicationDbContext>();

    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    // --- Thêm các dòng này ---
    // Đọc cấu hình SmtpSettings
    builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
    builder.Services.Configure<FeatureSettings>(builder.Configuration.GetSection("FeatureSettings"));
    // Đăng ký EmailService
    builder.Services.AddScoped<IEmailService, EmailService>();

    // Đăng ký ADAuthenticationService để sử dụng trong ứng dụng
    builder.Services.AddScoped<ADPasswordManager.Services.ADAuthenticationService>();
    builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
    builder.Services.AddHostedService<TokenCleanupService>();
    builder.Services.AddScoped<ISqlManagementService, SqlManagementService>();
    builder.Services.AddScoped<ADPasswordManager.Services.ADManagementService>();


    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    // Yêu cầu pipeline sử dụng Serilog để log các request HTTP
    app.UseSerilogRequestLogging();

    // Thêm dòng này để seed data khi ứng dụng khởi động
    DataSeeder.Seed(app);

    app.UseHttpsRedirection();
    app.UseStaticFiles();



    app.UseRouting();

    app.UseAuthentication(); // Thêm dòng này
    app.UseAuthorization();

    app.MapControllerRoute(
     name: "default",
     pattern: "{controller=Management}/{action=Index}/{id?}");

    //app.MapControllerRoute(
    //name: "default",
    //pattern: "{controller=Home}/{action=Index}/{id?}"); // <-- Sửa lại là Home
    app.MapRazorPages(); // Thêm dòng này để các trang Identity hoạt động

    app.Run();
}
catch (Exception ex)
{
    // Ghi lại lỗi nghiêm trọng nếu ứng dụng không thể khởi động
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // Đảm bảo các log cuối cùng được ghi lại trước khi ứng dụng đóng
    Log.CloseAndFlush();
}