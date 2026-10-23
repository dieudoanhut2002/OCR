namespace OcrWebApp.Services.Abstractions;

public record OcrWordBox(string Text, int X, int Y, int Width, int Height, float Confidence);

public record OcrEngineResult(string Text, float Confidence, List<OcrWordBox> Words);

public interface IOcrEngine
{
    /// <summary>
    /// Processes an image byte buffer and returns the extracted text and confidence scores.
    /// </summary>
    /// <param name="imageBytes">Preprocessed or raw image bytes (PNG, JPEG, TIFF).</param>
    /// <param name="language">Language code: "vie", "eng", or "vie+eng".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OcrEngineResult> ProcessImageAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the engine and required language models are initialized and ready.
    /// </summary>
    bool IsLanguageAvailable(string language);
}
