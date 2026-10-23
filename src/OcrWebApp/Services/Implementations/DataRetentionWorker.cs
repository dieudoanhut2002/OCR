using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcrWebApp.Data;
using OcrWebApp.Options;
using OcrWebApp.Services.Abstractions;

namespace OcrWebApp.Services.Implementations;

public class DataRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OcrSettings _settings;
    private readonly ILogger<DataRetentionWorker> _logger;

    public DataRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OcrSettings> settings,
        ILogger<DataRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.AutoCleanupEnabled)
        {
            _logger.LogInformation("Data Retention Auto-Cleanup đang tắt.");
            return;
        }

        _logger.LogInformation("Data Retention Worker đã kích hoạt. Thời gian lưu trữ: {Hours} giờ. Chu kỳ quét: {Interval} phút.",
            _settings.RetentionHours, _settings.CleanupIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong tiến trình dọn dẹp dữ liệu hết hạn định kỳ.");
            }

            var delayMinutes = Math.Max(5, _settings.CleanupIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
        }
    }

    private async Task CleanupExpiredJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fileSecurity = scope.ServiceProvider.GetRequiredService<IFileSecurityService>();

        var cutoffTimeUtc = DateTime.UtcNow.AddHours(-_settings.RetentionHours);

        var expiredJobs = await db.Jobs
            .Include(j => j.Pages)
            .Where(j => j.CreatedAtUtc < cutoffTimeUtc)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(100) // Batch of 100
            .ToListAsync(cancellationToken);

        if (expiredJobs.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Phát hiện {Count} công việc OCR đã hết hạn lưu trữ (>{Hours}h). Đang tiến hành xóa an toàn...",
            expiredJobs.Count, _settings.RetentionHours);

        int filesDeleted = 0;

        foreach (var job in expiredJobs)
        {
            if (!string.IsNullOrEmpty(job.StoredFileName))
            {
                fileSecurity.DeleteStoredFile(job.StoredFileName);
                filesDeleted++;
            }

            foreach (var page in job.Pages)
            {
                if (!string.IsNullOrEmpty(page.PageImagePath))
                {
                    fileSecurity.DeleteStoredFile(page.PageImagePath);
                    filesDeleted++;
                }
            }

            db.Jobs.Remove(job);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Đã dọn dẹp thành công {JobCount} công việc và {FileCount} file lưu trữ hết hạn.",
            expiredJobs.Count, filesDeleted);
    }
}
