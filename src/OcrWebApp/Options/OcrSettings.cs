namespace OcrWebApp.Options;

public class OcrSettings
{
    public const string SectionName = "OcrSettings";

    /// <summary>
    /// Relative or absolute path to private uploads directory.
    /// </summary>
    public string StoragePath { get; set; } = "App_Data/Uploads";

    /// <summary>
    /// Relative or absolute path to tessdata folder.
    /// </summary>
    public string TessdataPath { get; set; } = "tessdata";

    /// <summary>
    /// Max file size allowed in bytes (default: 25 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Maximum allowed pages in a single PDF or TIFF to prevent resource exhaustion.
    /// </summary>
    public int MaxPdfPages { get; set; } = 50;

    /// <summary>
    /// Retention period in hours before stored files and job history are automatically cleaned up.
    /// </summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>
    /// Whether background auto-cleanup of expired files is enabled.
    /// </summary>
    public bool AutoCleanupEnabled { get; set; } = true;

    /// <summary>
    /// Interval in minutes between periodic cleanup checks.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Timeout per job processing in seconds.
    /// </summary>
    public int ProcessingTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Max concurrent OCR processing tasks.
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 2;

    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".tiff", ".tif", ".pdf"];
}
