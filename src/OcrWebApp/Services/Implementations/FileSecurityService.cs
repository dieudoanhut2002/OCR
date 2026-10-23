using Microsoft.Extensions.Options;
using OcrWebApp.Options;
using OcrWebApp.Services.Abstractions;

namespace OcrWebApp.Services.Implementations;

public class FileSecurityService : IFileSecurityService
{
    private readonly OcrSettings _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileSecurityService> _logger;
    private readonly string _storageDirectory;

    public FileSecurityService(
        IOptions<OcrSettings> settings,
        IWebHostEnvironment env,
        ILogger<FileSecurityService> logger)
    {
        _settings = settings.Value;
        _env = env;
        _logger = logger;

        _storageDirectory = Path.IsPathRooted(_settings.StoragePath)
            ? _settings.StoragePath
            : Path.Combine(_env.ContentRootPath, _settings.StoragePath);

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public (bool IsValid, string DetectedContentType, string? ErrorMessage) ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, string.Empty, "File tải lên không có dữ liệu hoặc rỗng.");
        }

        if (file.Length > _settings.MaxFileSizeBytes)
        {
            var maxMb = _settings.MaxFileSizeBytes / (1024 * 1024);
            return (false, string.Empty, $"Dung lượng file vượt quá giới hạn cho phép ({maxMb} MB).");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_settings.AllowedExtensions.Contains(extension))
        {
            return (false, string.Empty, $"Định dạng file '{extension}' không được hỗ trợ. Chỉ chấp nhận JPG, PNG, TIFF, PDF.");
        }

        // Validate file header magic numbers
        using var stream = file.OpenReadStream();
        var header = new byte[8];
        var bytesRead = stream.Read(header, 0, header.Length);

        if (bytesRead < 4)
        {
            return (false, string.Empty, "File không hợp lệ hoặc bị hỏng.");
        }

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return (true, "image/jpeg", null);
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytesRead >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return (true, "image/png", null);
        }

        // TIFF Little Endian: 49 49 2A 00 or Big Endian: 4D 4D 00 2A
        if ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
            (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A))
        {
            return (true, "image/tiff", null);
        }

        // PDF: 25 50 44 46 (%PDF)
        if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
        {
            return (true, "application/pdf", null);
        }

        _logger.LogWarning("File '{FileName}' có extension '{Extension}' nhưng header magic bytes không khớp.",
            file.FileName, extension);

        return (false, string.Empty, "Nội dung file thực tế không khớp với phần mở rộng hoặc là file không an toàn.");
    }

    public async Task<string> SaveUploadedFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = GetSafeStoragePath(safeFileName);

        await using (var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(targetStream, cancellationToken);
        }

        return safeFileName;
    }

    public async Task<string> SaveFileBytesAsync(byte[] bytes, string extension, CancellationToken cancellationToken = default)
    {
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = GetSafeStoragePath(safeFileName);

        await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);
        return safeFileName;
    }

    public string GetSafeStoragePath(string storedFileName)
    {
        // Prevent path traversal
        var cleanFileName = Path.GetFileName(storedFileName);
        var combinedPath = Path.GetFullPath(Path.Combine(_storageDirectory, cleanFileName));

        var normalizedStorage = Path.GetFullPath(_storageDirectory);
        if (!combinedPath.StartsWith(normalizedStorage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Phát hiện hành vi truy cập thư mục trái phép (Path Traversal attempt).");
        }

        return combinedPath;
    }

    public void DeleteStoredFile(string storedFileName)
    {
        try
        {
            var path = GetSafeStoragePath(storedFileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa file lưu trữ: {StoredFileName}", storedFileName);
        }
    }

    public bool FileExists(string storedFileName)
    {
        try
        {
            var path = GetSafeStoragePath(storedFileName);
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}
