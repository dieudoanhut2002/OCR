namespace OcrWebApp.Models.Entities;

public class OcrJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Cryptographic session token stored in user's cookie to isolate job ownership.
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Safe GUID-based filename in private storage.
    /// </summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// "vie", "eng", or "vie+eng"
    /// </summary>
    public string Language { get; set; } = "vie";

    public OcrJobStatus Status { get; set; } = OcrJobStatus.Pending;

    public int TotalPages { get; set; } = 1;

    public int ProcessedPages { get; set; } = 0;

    public float? AverageConfidence { get; set; }

    /// <summary>
    /// Combined full document text.
    /// </summary>
    public string? ExtractedText { get; set; }

    public string? ErrorMessage { get; set; }

    public string PageRange { get; set; } = "all";

    public bool PreprocessGrayscale { get; set; } = true;
    public bool PreprocessContrast { get; set; } = true;
    public bool PreprocessDeskew { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<OcrJobPage> Pages { get; set; } = new List<OcrJobPage>();
}
