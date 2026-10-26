using System.ComponentModel.DataAnnotations;

namespace OcrWebApp.Models.DTOs;

public class UploadRequestDto
{
    [Required(ErrorMessage = "Vui lòng chọn ít nhất một file.")]
    public List<IFormFile> Files { get; set; } = new();

    public string Language { get; set; } = "vie";

    /// <summary>
    /// Page range for PDF/TIFF, e.g. "1-5", "1, 3, 5", or "all" (default).
    /// </summary>
    public string PageRange { get; set; } = "all";

    public bool PreprocessGrayscale { get; set; } = true;
    public bool PreprocessContrast { get; set; } = true;
    public bool PreprocessDeskew { get; set; } = false;
}
