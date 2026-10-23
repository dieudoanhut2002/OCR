using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcrWebApp.Data;
using OcrWebApp.Middlewares;
using OcrWebApp.Models.DTOs;
using OcrWebApp.Models.Entities;
using OcrWebApp.Models.ViewModels;
using OcrWebApp.Services.Abstractions;

namespace OcrWebApp.Controllers;

public class JobController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFileSecurityService _fileSecurity;
    private readonly IBackgroundOcrQueue _queue;
    private readonly IExportService _exportService;
    private readonly ILogger<JobController> _logger;

    public JobController(
        AppDbContext db,
        IFileSecurityService fileSecurity,
        IBackgroundOcrQueue queue,
        IExportService exportService,
        ILogger<JobController> logger)
    {
        _db = db;
        _fileSecurity = fileSecurity;
        _queue = queue;
        _exportService = exportService;
        _logger = logger;
    }

    private string GetSessionToken()
    {
        return HttpContext.Items[UserSessionMiddleware.SessionItemKey]?.ToString() ?? string.Empty;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] UploadRequestDto dto)
    {
        if (dto.Files == null || dto.Files.Count == 0)
        {
            return BadRequest(new { success = false, message = "Vui lòng chọn ít nhất một file ảnh hoặc PDF." });
        }

        var sessionToken = GetSessionToken();
        var createdJobs = new List<JobStatusResponseDto>();
        var errors = new List<string>();

        foreach (var file in dto.Files)
        {
            var (isValid, contentType, errorMessage) = _fileSecurity.ValidateFile(file);
            if (!isValid)
            {
                errors.Add($"{file.FileName}: {errorMessage}");
                continue;
            }

            try
            {
                var storedFileName = await _fileSecurity.SaveUploadedFileAsync(file);

                var job = new OcrJob
                {
                    SessionToken = sessionToken,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    StoredFileName = storedFileName,
                    ContentType = contentType,
                    FileSizeBytes = file.Length,
                    Language = string.IsNullOrWhiteSpace(dto.Language) ? "vie" : dto.Language,
                    PageRange = dto.PageRange,
                    Status = OcrJobStatus.Pending,
                    TotalPages = 1,
                    ProcessedPages = 0,
                    PreprocessGrayscale = dto.PreprocessGrayscale,
                    PreprocessContrast = dto.PreprocessContrast,
                    PreprocessDeskew = dto.PreprocessDeskew,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.Jobs.Add(job);
                await _db.SaveChangesAsync();

                // Enqueue job for background execution
                await _queue.QueueJobAsync(job.Id);

                createdJobs.Add(new JobStatusResponseDto
                {
                    Id = job.Id,
                    FileName = job.OriginalFileName,
                    Status = job.Status.ToString(),
                    StatusNumber = (int)job.Status,
                    TotalPages = job.TotalPages,
                    ProcessedPages = job.ProcessedPages,
                    CreatedAt = job.CreatedAtUtc.ToString("HH:mm:ss dd/MM/yyyy")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu trữ file upload {FileName}", file.FileName);
                errors.Add($"{file.FileName}: Lỗi xử lý lưu trữ file trên máy chủ.");
            }
        }

        return Json(new
        {
            success = createdJobs.Count > 0,
            jobs = createdJobs,
            errors
        });
    }

    [HttpGet]
    public async Task<IActionResult> Status(Guid id)
    {
        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.SessionToken == sessionToken);

        if (job == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy công việc OCR hoặc bạn không có quyền truy cập." });
        }

        return Json(new JobStatusResponseDto
        {
            Id = job.Id,
            FileName = job.OriginalFileName,
            Status = job.Status.ToString(),
            StatusNumber = (int)job.Status,
            TotalPages = job.TotalPages,
            ProcessedPages = job.ProcessedPages,
            AverageConfidence = job.AverageConfidence,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAtUtc.ToString("HH:mm:ss dd/MM/yyyy"),
            CompletedAt = job.CompletedAtUtc?.ToString("HH:mm:ss dd/MM/yyyy")
        });
    }

    [HttpGet]
    public async Task<IActionResult> BatchStatus(string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Json(new List<JobStatusResponseDto>());
        }

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var sessionToken = GetSessionToken();
        var jobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => idList.Contains(j.Id) && j.SessionToken == sessionToken)
            .ToListAsync();

        var result = jobs.Select(job => new JobStatusResponseDto
        {
            Id = job.Id,
            FileName = job.OriginalFileName,
            Status = job.Status.ToString(),
            StatusNumber = (int)job.Status,
            TotalPages = job.TotalPages,
            ProcessedPages = job.ProcessedPages,
            AverageConfidence = job.AverageConfidence,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAtUtc.ToString("HH:mm:ss dd/MM/yyyy"),
            CompletedAt = job.CompletedAtUtc?.ToString("HH:mm:ss dd/MM/yyyy")
        }).ToList();

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, int page = 1)
    {
        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .Include(j => j.Pages)
            .FirstOrDefaultAsync(j => j.Id == id && j.SessionToken == sessionToken);

        if (job == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy công việc OCR hoặc bạn không có quyền truy cập.";
            return RedirectToAction("Index", "Home");
        }

        if (page < 1) page = 1;
        if (job.TotalPages > 0 && page > job.TotalPages) page = job.TotalPages;

        var vm = new JobDetailViewModel
        {
            Job = job,
            CurrentPageNumber = page
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> PageImage(Guid id, int page = 1)
    {
        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .Include(j => j.Pages)
            .FirstOrDefaultAsync(j => j.Id == id && j.SessionToken == sessionToken);

        if (job == null)
        {
            return NotFound();
        }

        var targetPage = job.Pages.FirstOrDefault(p => p.PageNumber == page);
        if (targetPage == null || string.IsNullOrEmpty(targetPage.PageImagePath))
        {
            // If single image job without separate page image, return original file if image
            if (!job.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                var originalPath = _fileSecurity.GetSafeStoragePath(job.StoredFileName);
                if (System.IO.File.Exists(originalPath))
                {
                    return PhysicalFile(originalPath, job.ContentType);
                }
            }
            return NotFound();
        }

        var imagePath = _fileSecurity.GetSafeStoragePath(targetPage.PageImagePath);
        if (!System.IO.File.Exists(imagePath))
        {
            return NotFound();
        }

        return PhysicalFile(imagePath, "image/png");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveEditedTextRequest req)
    {
        if (req == null)
        {
            return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .Include(j => j.Pages)
            .FirstOrDefaultAsync(j => j.Id == req.JobId && j.SessionToken == sessionToken);

        if (job == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy tài liệu hoặc không có quyền chỉnh sửa." });
        }

        if (req.PageNumber.HasValue)
        {
            var page = job.Pages.FirstOrDefault(p => p.PageNumber == req.PageNumber.Value);
            if (page != null)
            {
                page.ExtractedText = req.Content ?? string.Empty;
                page.UpdatedAtUtc = DateTime.UtcNow;

                // Re-sync full document text
                var fullText = string.Join("\n\n--- Trang {0} ---\n\n",
                    job.Pages.OrderBy(p => p.PageNumber).Select(p => p.ExtractedText));
                job.ExtractedText = fullText;
            }
            else
            {
                return NotFound(new { success = false, message = $"Không tìm thấy trang số {req.PageNumber}." });
            }
        }
        else
        {
            job.ExtractedText = req.Content ?? string.Empty;
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã lưu nội dung chỉnh sửa thành công!" });
    }

    [HttpGet]
    public async Task<IActionResult> Export(Guid id, string format = "txt")
    {
        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .Include(j => j.Pages)
            .FirstOrDefaultAsync(j => j.Id == id && j.SessionToken == sessionToken);

        if (job == null)
        {
            return NotFound("Không tìm thấy tài liệu.");
        }

        var safeBaseName = Path.GetFileNameWithoutExtension(job.OriginalFileName);
        var cleanBaseName = string.Join("_", safeBaseName.Split(Path.GetInvalidFileNameChars()));

        switch (format.ToLowerInvariant())
        {
            case "docx":
            case "doc":
                var docxBytes = _exportService.ExportToDocx(job);
                return File(docxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{cleanBaseName}_OCR.docx");

            case "pdf":
                var pdfBytes = _exportService.ExportToPdf(job);
                return File(pdfBytes, "application/pdf", $"{cleanBaseName}_OCR.pdf");

            case "txt":
            default:
                var txtBytes = _exportService.ExportToTxt(job);
                return File(txtBytes, "text/plain; charset=utf-8", $"{cleanBaseName}_OCR.txt");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(Guid id)
    {
        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .FirstOrDefaultAsync(j => j.Id == id && j.SessionToken == sessionToken);

        if (job == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy công việc." });
        }

        job.Status = OcrJobStatus.Pending;
        job.ErrorMessage = null;
        job.ProcessedPages = 0;
        await _db.SaveChangesAsync();

        await _queue.QueueJobAsync(job.Id);

        return Json(new { success = true, message = "Đã đưa công việc vào hàng đợi xử lý lại." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sessionToken = GetSessionToken();
        var job = await _db.Jobs
            .Include(j => j.Pages)
            .FirstOrDefaultAsync(j => j.Id == id && j.SessionToken == sessionToken);

        if (job == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy công việc hoặc bạn không có quyền xóa." });
        }

        // Delete physical files
        if (!string.IsNullOrEmpty(job.StoredFileName))
        {
            _fileSecurity.DeleteStoredFile(job.StoredFileName);
        }

        foreach (var page in job.Pages)
        {
            if (!string.IsNullOrEmpty(page.PageImagePath))
            {
                _fileSecurity.DeleteStoredFile(page.PageImagePath);
            }
        }

        _db.Jobs.Remove(job);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Đã xóa công việc OCR và các file liên quan thành công." });
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var sessionToken = GetSessionToken();
        var jobs = await _db.Jobs
            .Where(j => j.SessionToken == sessionToken)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync();

        var vm = new JobHistoryViewModel
        {
            Jobs = jobs
        };

        return View(vm);
    }
}
