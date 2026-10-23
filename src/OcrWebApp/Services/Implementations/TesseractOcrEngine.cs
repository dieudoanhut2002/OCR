using Microsoft.Extensions.Options;
using OcrWebApp.Options;
using OcrWebApp.Services.Abstractions;
using Tesseract;

namespace OcrWebApp.Services.Implementations;

public class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private readonly OcrSettings _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TesseractOcrEngine> _logger;
    private readonly string _tessdataPath;

    public TesseractOcrEngine(
        IOptions<OcrSettings> settings,
        IWebHostEnvironment env,
        ILogger<TesseractOcrEngine> logger)
    {
        _settings = settings.Value;
        _env = env;
        _logger = logger;

        _tessdataPath = Path.IsPathRooted(_settings.TessdataPath)
            ? _settings.TessdataPath
            : Path.Combine(_env.ContentRootPath, _settings.TessdataPath);

        EnsureTessdataDirectory();
    }

    private void EnsureTessdataDirectory()
    {
        if (!Directory.Exists(_tessdataPath))
        {
            Directory.CreateDirectory(_tessdataPath);
        }

        // Check if standard models exist
        var engExists = File.Exists(Path.Combine(_tessdataPath, "eng.traineddata"));
        var vieExists = File.Exists(Path.Combine(_tessdataPath, "vie.traineddata"));

        if (!engExists || !vieExists)
        {
            _logger.LogWarning("Tessdata thiếu file ngôn ngữ tại '{Path}'. eng: {Eng}, vie: {Vie}",
                _tessdataPath, engExists, vieExists);
        }
    }

    public bool IsLanguageAvailable(string language)
    {
        var langs = language.Split('+');
        foreach (var lang in langs)
        {
            var path = Path.Combine(_tessdataPath, $"{lang.Trim()}.traineddata");
            if (!File.Exists(path))
            {
                return false;
            }
        }
        return true;
    }

    public Task<OcrEngineResult> ProcessImageAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedLang = string.IsNullOrWhiteSpace(language) ? "vie" : language.Trim();

            // Fallback if specific traineddata is missing
            if (!IsLanguageAvailable(normalizedLang))
            {
                if (IsLanguageAvailable("eng"))
                {
                    _logger.LogWarning("Ngôn ngữ '{Lang}' không có file traineddata, tạm fallback về 'eng'.", normalizedLang);
                    normalizedLang = "eng";
                }
                else
                {
                    throw new FileNotFoundException($"Không tìm thấy file mô hình ngôn ngữ Tesseract cho '{normalizedLang}' tại '{_tessdataPath}'.");
                }
            }

            using var engine = new TesseractEngine(_tessdataPath, normalizedLang, EngineMode.Default);
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);

            var text = page.GetText()?.Trim() ?? string.Empty;
            var confidence = page.GetMeanConfidence() * 100f; // Scale to 0-100%

            var words = new List<OcrWordBox>();
            try
            {
                using var iter = page.GetIterator();
                iter.Begin();
                do
                {
                    if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                    {
                        var wordText = iter.GetText(PageIteratorLevel.Word);
                        var wordConf = iter.GetConfidence(PageIteratorLevel.Word);
                        if (!string.IsNullOrWhiteSpace(wordText))
                        {
                            words.Add(new OcrWordBox(
                                wordText,
                                rect.X1,
                                rect.Y1,
                                rect.Width,
                                rect.Height,
                                wordConf
                            ));
                        }
                    }
                } while (iter.Next(PageIteratorLevel.Word));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Không thể lấy bounding boxes chi tiết từ trang OCR.");
            }

            return new OcrEngineResult(text, confidence, words);
        }, cancellationToken);
    }

    public void Dispose()
    {
        // Engine is instantiated per operation or pooled
        GC.SuppressFinalize(this);
    }
}
