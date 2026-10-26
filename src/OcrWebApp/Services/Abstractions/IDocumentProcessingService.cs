namespace OcrWebApp.Services.Abstractions;

public record OcrProcessingOptions(
    string Language,
    bool PreprocessGrayscale,
    bool PreprocessContrast,
    bool PreprocessDeskew,
    string PageRange = "all"
);

public record ProcessedPageResult(
    int PageNumber,
    string Text,
    float Confidence,
    bool IsTextLayerDirect,
    byte[]? PagePreviewPngBytes,
    string? BoundingBoxesJson
);

public record ProcessedDocumentResult(
    int TotalPages,
    List<ProcessedPageResult> Pages,
    string FullText,
    float AverageConfidence
);

public interface IDocumentProcessingService
{
    /// <summary>
    /// Processes a document (PDF or Image), performing text layer extraction or page rendering and OCR.
    /// Calls progress callback as each page finishes.
    /// </summary>
    Task<ProcessedDocumentResult> ProcessDocumentAsync(
        string storedFilePath,
        string contentType,
        OcrProcessingOptions options,
        Func<int, int, Task>? onPageProgress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preprocesses image bytes (grayscale, contrast, auto-deskew).
    /// </summary>
    byte[] PreprocessImage(byte[] imageBytes, bool grayscale, bool contrast, bool deskew);
}
