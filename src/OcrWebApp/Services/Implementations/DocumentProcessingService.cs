using Microsoft.Extensions.Options;
using OcrWebApp.Options;
using OcrWebApp.Services.Abstractions;
using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OcrWebApp.Services.Implementations;

public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IOcrEngine _ocrEngine;
    private readonly OcrSettings _settings;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        IOcrEngine ocrEngine,
        IOptions<OcrSettings> settings,
        ILogger<DocumentProcessingService> logger)
    {
        _ocrEngine = ocrEngine;
        _settings = settings.Value;
        _logger = logger;
    }

    public byte[] PreprocessImage(byte[] imageBytes, bool grayscale, bool contrast, bool deskew)
    {
        try
        {
            using Image image = Image.Load(imageBytes);

            image.Mutate(ctx =>
            {
                if (deskew)
                {
                    ctx.AutoOrient();
                }

                if (grayscale)
                {
                    ctx.Grayscale();
                }

                if (contrast)
                {
                    ctx.Contrast(1.25f);
                }
            });

            using MemoryStream outStream = new MemoryStream();
            image.SaveAsPng(outStream);
            return outStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tiền xử lý ảnh thất bại, dùng ảnh gốc để tiếp tục.");
            return imageBytes;
        }
    }

    public async Task<ProcessedDocumentResult> ProcessDocumentAsync(
        string storedFilePath,
        string contentType,
        OcrProcessingOptions options,
        Func<int, int, Task>? onPageProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storedFilePath))
        {
            throw new FileNotFoundException($"Không tìm thấy file tài liệu tại '{storedFilePath}'.");
        }

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessPdfDocumentAsync(storedFilePath, options, onPageProgress, cancellationToken);
        }

        // Handle Images (PNG, JPEG, TIFF)
        return await ProcessImageDocumentAsync(storedFilePath, contentType, options, onPageProgress, cancellationToken);
    }

    private async Task<ProcessedDocumentResult> ProcessPdfDocumentAsync(
        string storedFilePath,
        OcrProcessingOptions options,
        Func<int, int, Task>? onPageProgress,
        CancellationToken cancellationToken)
    {
        List<ProcessedPageResult> pagesResult = new List<ProcessedPageResult>();

        // 1. Inspect PDF structure with PdfPig to check total pages & text layer
        using PdfDocument pdfPigDoc = PdfDocument.Open(storedFilePath);
        var totalPages = pdfPigDoc.NumberOfPages;

        if (totalPages > _settings.MaxPdfPages)
        {
            throw new InvalidOperationException($"Tài liệu PDF có {totalPages} trang, vượt quá giới hạn cho phép ({_settings.MaxPdfPages} trang).");
        }

        // Cache existing text layers
        List<Page> pdfPigPages = pdfPigDoc.GetPages().ToList();

        // 2. Render each page using PDFtoImage
        await using var pdfFileStream = File.OpenRead(storedFilePath);
        List<SKBitmap> renderedImages = Conversion.ToImages(pdfFileStream).ToList();

        var allowedPages = ParsePageRange(options.PageRange, totalPages);

        for (int i = 0; i < totalPages; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageIndex = i;
            var pageNumber = i + 1;

            if (allowedPages != null && !allowedPages.Contains(pageNumber))
            {
                continue;
            }

            // Render preview PNG bytes
            byte[] pagePreviewPngBytes;
            using (var skBitmap = renderedImages[pageIndex])
            using (SKImage image = SKImage.FromBitmap(skBitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 95))
            {
                pagePreviewPngBytes = data.ToArray();
            }

            // Check if page has selectable text layer
            var pigPage = pdfPigPages[pageIndex];
            var pageLayerText = pigPage.Text?.Trim();

            // A page has selectable text if it contains meaningful words (> 15 chars)
            bool hasValidTextLayer = !string.IsNullOrWhiteSpace(pageLayerText) && pageLayerText.Length >= 15;

            string pageText;
            float pageConfidence;
            bool isDirectTextLayer;
            string? boundingBoxesJson = null;

            if (hasValidTextLayer)
            {
                // Direct text extraction from PDF layer
                pageText = pageLayerText!;
                pageConfidence = 100.0f;
                isDirectTextLayer = true;
            }
            else
            {
                // Perform OCR on rendered page image
                var processedBytes = PreprocessImage(
                    pagePreviewPngBytes,
                    options.PreprocessGrayscale,
                    options.PreprocessContrast,
                    options.PreprocessDeskew);

                var ocrResult = await _ocrEngine.ProcessImageAsync(processedBytes, options.Language, cancellationToken);
                pageText = ocrResult.Text;
                pageConfidence = ocrResult.Confidence;
                isDirectTextLayer = false;

                if (ocrResult.Words.Count > 0)
                {
                    boundingBoxesJson = JsonSerializer.Serialize(ocrResult.Words);
                }
            }

            pagesResult.Add(new ProcessedPageResult(
                pageNumber,
                pageText,
                pageConfidence,
                isDirectTextLayer,
                pagePreviewPngBytes,
                boundingBoxesJson
            ));

            if (onPageProgress != null)
            {
                await onPageProgress(pageNumber, totalPages);
            }
        }

        StringBuilder fullTextBuilder = new StringBuilder();
        foreach (var p in pagesResult)
        {
            if (fullTextBuilder.Length > 0)
            {
                fullTextBuilder.AppendLine();
                fullTextBuilder.AppendLine($"--- Trang {p.PageNumber} ---");
                fullTextBuilder.AppendLine();
            }
            fullTextBuilder.AppendLine(p.Text);
        }

        var avgConfidence = pagesResult.Count > 0
            ? (float)Math.Round(pagesResult.Average(p => p.Confidence), 1)
            : 0f;

        return new ProcessedDocumentResult(totalPages, pagesResult, fullTextBuilder.ToString().Trim(), avgConfidence);
    }

    private async Task<ProcessedDocumentResult> ProcessImageDocumentAsync(
        string storedFilePath,
        string contentType,
        OcrProcessingOptions options,
        Func<int, int, Task>? onPageProgress,
        CancellationToken cancellationToken)
    {
        var rawBytes = await File.ReadAllBytesAsync(storedFilePath, cancellationToken);
        List<ProcessedPageResult> pagesResult = new List<ProcessedPageResult>();

        // Check if multi-frame TIFF
        using Image imageInfo = Image.Load(rawBytes);
        var frameCount = imageInfo.Frames.Count;

        if (frameCount > 1)
        {
            // Multi-page TIFF
            if (frameCount > _settings.MaxPdfPages)
            {
                throw new InvalidOperationException($"File TIFF có {frameCount} trang, vượt quá giới hạn cho phép ({_settings.MaxPdfPages} trang).");
            }

            for (int i = 0; i < frameCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageNumber = i + 1;

                using var singleFrame = imageInfo.Frames.CloneFrame(i);
                using MemoryStream ms = new MemoryStream();
                await singleFrame.SaveAsPngAsync(ms, cancellationToken);
                var framePngBytes = ms.ToArray();

                var processedBytes = PreprocessImage(
                    framePngBytes,
                    options.PreprocessGrayscale,
                    options.PreprocessContrast,
                    options.PreprocessDeskew);

                var ocrResult = await _ocrEngine.ProcessImageAsync(processedBytes, options.Language, cancellationToken);

                string? boundingBoxesJson = ocrResult.Words.Count > 0
                    ? JsonSerializer.Serialize(ocrResult.Words)
                    : null;

                pagesResult.Add(new ProcessedPageResult(
                    pageNumber,
                    ocrResult.Text,
                    ocrResult.Confidence,
                    false,
                    framePngBytes,
                    boundingBoxesJson
                ));

                if (onPageProgress != null)
                {
                    await onPageProgress(pageNumber, frameCount);
                }
            }
        }
        else
        {
            // Single page Image (JPG, PNG, single-page TIFF)
            cancellationToken.ThrowIfCancellationRequested();

            var processedBytes = PreprocessImage(
                rawBytes,
                options.PreprocessGrayscale,
                options.PreprocessContrast,
                options.PreprocessDeskew);

            var ocrResult = await _ocrEngine.ProcessImageAsync(processedBytes, options.Language, cancellationToken);

            string? boundingBoxesJson = ocrResult.Words.Count > 0
                ? JsonSerializer.Serialize(ocrResult.Words)
                : null;

            pagesResult.Add(new ProcessedPageResult(
                1,
                ocrResult.Text,
                ocrResult.Confidence,
                false,
                rawBytes,
                boundingBoxesJson
            ));

            if (onPageProgress != null)
            {
                await onPageProgress(1, 1);
            }
        }

        StringBuilder fullTextBuilder = new StringBuilder();
        foreach (var p in pagesResult)
        {
            if (fullTextBuilder.Length > 0 && pagesResult.Count > 1)
            {
                fullTextBuilder.AppendLine();
                fullTextBuilder.AppendLine($"--- Trang {p.PageNumber} ---");
                fullTextBuilder.AppendLine();
            }
            fullTextBuilder.AppendLine(p.Text);
        }

        var avgConfidence = pagesResult.Count > 0
            ? (float)Math.Round(pagesResult.Average(p => p.Confidence), 1)
            : 0f;

        return new ProcessedDocumentResult(pagesResult.Count, pagesResult, fullTextBuilder.ToString().Trim(), avgConfidence);
    }

    private static HashSet<int>? ParsePageRange(string? rangeStr, int totalPages)
    {
        if (string.IsNullOrWhiteSpace(rangeStr) || rangeStr.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        HashSet<int> result = new HashSet<int>();
        var parts = rangeStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-'))
            {
                var rangeParts = trimmed.Split('-');
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out var start) &&
                    int.TryParse(rangeParts[1], out var end))
                {
                    for (int p = Math.Max(1, start); p <= Math.Min(totalPages, end); p++)
                    {
                        result.Add(p);
                    }
                }
            }
            else if (int.TryParse(trimmed, out var page))
            {
                if (page >= 1 && page <= totalPages)
                {
                    result.Add(page);
                }
            }
        }

        return result.Count > 0 ? result : null;
    }
}
