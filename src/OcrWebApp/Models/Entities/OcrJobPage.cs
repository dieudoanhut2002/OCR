namespace OcrWebApp.Models.Entities;

public class OcrJobPage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }
    public OcrJob Job { get; set; } = null!;

    public int PageNumber { get; set; }

    /// <summary>
    /// Stored file name of the rendered page image (used for viewer display).
    /// </summary>
    public string? PageImagePath { get; set; }

    /// <summary>
    /// Text extracted for this page, supports user edits.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// OCR confidence score (0 to 100%).
    /// </summary>
    public float? Confidence { get; set; }

    /// <summary>
    /// Whether this page was extracted directly from an existing PDF text layer.
    /// </summary>
    public bool IsTextLayerDirect { get; set; }

    /// <summary>
    /// JSON serialization of bounding boxes for words/lines.
    /// </summary>
    public string? BoundingBoxesJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
