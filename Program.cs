using ADPasswordManager.Data;
using ADPasswordManager.Models.Configuration;
using ADPasswordManager.Services;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using Serilog;


// Đọc cấu hình tạm thời để lấy đường dẫn log cho bootstrap logger
var tempConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Lấy đường dẫn log từ config, nếu không có thì dùng đường dẫn tương đối
var bootstrapLogPath = tempConfig["LoggingSettings:LogFilePath"] ?? Path.Combine(AppContext.BaseDirectory, "logs/ad-password-manager-.txt");


// Cấu hình logger của Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(bootstrapLogPath, rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();




Log.Information("Starting up the application");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Bảo với host sử dụng Serilog thay vì logger mặc định
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        // --- THAY ĐỔI Ở ĐÂY (Main Logger) ---

        // Đọc đường dẫn log từ file cấu hình
        // Dùng giá trị của bootstrap làm fallback nếu không tìm thấy key
        var logPath = context.Configuration["LoggingSettings:LogFilePath"] ?? bootstrapLogPath;

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day); // <-- Sử dụng biến logPath

        // --- KẾT THÚC THAY ĐỔI ---
    });

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