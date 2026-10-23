using OcrWebApp.Models.Entities;

namespace OcrWebApp.Services.Abstractions;

public interface IExportService
{
    /// <summary>
    /// Generates UTF-8 encoded plain text file with BOM for cross-platform editors.
    /// </summary>
    byte[] ExportToTxt(OcrJob job);

    /// <summary>
    /// Generates a valid Microsoft Word (.docx) document with proper styling and page breaks.
    /// </summary>
    byte[] ExportToDocx(OcrJob job);

    /// <summary>
    /// Generates a PDF containing the recognized text formatted per page.
    /// </summary>
    byte[] ExportToPdf(OcrJob job);
}
