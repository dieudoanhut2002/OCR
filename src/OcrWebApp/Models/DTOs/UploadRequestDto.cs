using System.ComponentModel.DataAnnotations;

namespace OcrWebApp.Models.DTOs;

public class UploadRequestDto
{
    [Required(ErrorMessage = "Vui lòng chọn ít nhất một file.")]
    public List<IFormFile> Files { get; set; } = new();

    public string Language { get; set; } = "vie";

    /// <summary>
    /// Optional page range for PDF/TIFF, e.g. "1-5", "1, 3, 5". If empty, processes all pages up to MaxPdfPages.
    /// </summary>
    public string? PageRange { get; set; }

    public bool PreprocessGrayscale { get; set; } = true;
    public bool PreprocessContrast { get; set; } = true;
    public bool PreprocessDeskew { get; set; } = false;
}
