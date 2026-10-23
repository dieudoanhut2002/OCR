using Microsoft.EntityFrameworkCore;
using OcrWebApp.Data;
using OcrWebApp.Middlewares;
using OcrWebApp.Options;
using OcrWebApp.Services.Abstractions;
using OcrWebApp.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration & Options
builder.Services.Configure<OcrSettings>(builder.Configuration.GetSection(OcrSettings.SectionName));

// 2. Database with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=OcrDocumentDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });
});

// 3. Dependency Injection Services
builder.Services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
builder.Services.AddSingleton<IFileSecurityService, FileSecurityService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IExportService, ExportService>();

// 4. Background OCR Queue & Workers
builder.Services.AddSingleton<IBackgroundOcrQueue, BackgroundOcrQueue>();
builder.Services.AddHostedService<BackgroundOcrWorker>();
builder.Services.AddHostedService<DataRetentionWorker>();

// 5. MVC & Antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 6. Automatic Database Migration / Initialization
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        logger.LogInformation("Kiểm tra và áp dụng EF Core Migrations cho SQL Server...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Cơ sở dữ liệu SQL Server đã được cập nhật thành công.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Không thể kết nối hoặc tự động migrate cơ sở dữ liệu SQL Server. Ứng dụng sẽ tiếp tục khởi động. Vui lòng kiểm tra chuỗi kết nối DefaultConnection hoặc chạy lệnh: dotnet ef database update");
    }
}

// 7. HTTP Request Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// User session tracking for anonymous privacy protection
app.UseMiddleware<UserSessionMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
