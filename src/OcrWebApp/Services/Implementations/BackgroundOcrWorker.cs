using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcrWebApp.Data;
using OcrWebApp.Models.Entities;
using OcrWebApp.Options;
using OcrWebApp.Services.Abstractions;

namespace OcrWebApp.Services.Implementations;

public class BackgroundOcrWorker : BackgroundService
{
    private readonly IBackgroundOcrQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OcrSettings _settings;
    private readonly ILogger<BackgroundOcrWorker> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public BackgroundOcrWorker(
        IBackgroundOcrQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<OcrSettings> settings,
        ILogger<BackgroundOcrWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
        _concurrencySemaphore = new SemaphoreSlim(_settings.ConcurrencyLimit, _settings.ConcurrencyLimit);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background OCR Worker đã khởi động. Giới hạn đồng thời: {Limit}", _settings.ConcurrencyLimit);

        // Crash recovery: Re-queue pending and interrupted processing jobs on startup
        await RecoverPendingJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueJobAsync(stoppingToken);

                // Wait for concurrency slot
                await _concurrencySemaphore.WaitAsync(stoppingToken);

                // Fire-and-forget inside worker task with controlled concurrency
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessJobAsync(jobId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi không xác định khi xử lý job {JobId}", jobId);
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong vòng lặp tiếp nhận OCR queue.");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task RecoverPendingJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unfinishedJobs = await db.Jobs
                .Where(j => j.Status == OcrJobStatus.Pending || j.Status == OcrJobStatus.Processing)
                .OrderBy(j => j.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            if (unfinishedJobs.Count > 0)
            {
                _logger.LogInformation("Tìm thấy {Count} job chưa hoàn thành từ phiên làm việc trước. Đang đưa lại vào hàng đợi...", unfinishedJobs.Count);
                foreach (var job in unfinishedJobs)
                {
                    job.Status = OcrJobStatus.Pending;
                    await _queue.QueueJobAsync(job.Id, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi phục hồi các công việc pending từ database lúc khởi động.");
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var docService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
        var fileSecurity = scope.ServiceProvider.GetRequiredService<IFileSecurityService>();

        var job = await db.Jobs
            .Include(j => j.Pages)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Không tìm thấy job {JobId} trong database.", jobId);
            return;
        }

        try
        {
            job.Status = OcrJobStatus.Processing;
            job.StartedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var filePath = fileSecurity.GetSafeStoragePath(job.StoredFileName);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File gốc đã bị xóa hoặc không thể truy cập.");
            }

            var options = new OcrProcessingOptions(
                job.Language,
                job.PreprocessGrayscale,
                job.PreprocessContrast,
                job.PreprocessDeskew,
                job.PageRange
            );

            // Timeout token per job
            using var jobTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            jobTimeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.ProcessingTimeoutSeconds));

            var result = await docService.ProcessDocumentAsync(
                filePath,
                job.ContentType,
                options,
                async (pageCurrent, pageTotal) =>
                {
                    // Update progress periodically in DB
                    try
                    {
                        job.ProcessedPages = pageCurrent;
                        job.TotalPages = pageTotal;
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore progress save contention
                    }
                },
                jobTimeoutCts.Token);

            // Remove existing pages if retrying
            if (job.Pages.Count > 0)
            {
                foreach (var oldPage in job.Pages)
                {
                    if (!string.IsNullOrEmpty(oldPage.PageImagePath))
                    {
                        fileSecurity.DeleteStoredFile(oldPage.PageImagePath);
                    }
                }
                db.JobPages.RemoveRange(job.Pages);
            }

            // Save rendered preview pages to disk and add page records
            var jobPages = new List<OcrJobPage>();
            foreach (var pageResult in result.Pages)
            {
                string? pagePreviewFileName = null;
                if (pageResult.PagePreviewPngBytes != null && pageResult.PagePreviewPngBytes.Length > 0)
                {
                    pagePreviewFileName = await fileSecurity.SaveFileBytesAsync(
                        pageResult.PagePreviewPngBytes,
                        ".png",
                        cancellationToken);
                }

                jobPages.Add(new OcrJobPage
                {
                    JobId = job.Id,
                    PageNumber = pageResult.PageNumber,
                    PageImagePath = pagePreviewFileName,
                    ExtractedText = pageResult.Text,
                    Confidence = pageResult.Confidence,
                    IsTextLayerDirect = pageResult.IsTextLayerDirect,
                    BoundingBoxesJson = pageResult.BoundingBoxesJson,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            job.Pages = jobPages;
            job.TotalPages = result.TotalPages;
            job.ProcessedPages = result.TotalPages;
            job.AverageConfidence = result.AverageConfidence;
            job.ExtractedText = result.FullText;
            job.Status = OcrJobStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.ErrorMessage = null;

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Job OCR {JobId} hoàn tất thành công. Số trang: {Pages}, Confidence: {Conf}%",
                job.Id, job.TotalPages, job.AverageConfidence);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job OCR {JobId} bị hủy hoặc quá thời gian xử lý cho phép.", jobId);
            job.Status = OcrJobStatus.Failed;
            job.ErrorMessage = "Quá thời gian xử lý cho phép hoặc tác vụ bị hủy.";
            job.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý job OCR {JobId}", jobId);
            job.Status = OcrJobStatus.Failed;
            job.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            job.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
