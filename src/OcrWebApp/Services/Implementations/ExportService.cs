using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OcrWebApp.Models.Entities;
using OcrWebApp.Services.Abstractions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace OcrWebApp.Services.Implementations;

public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    public byte[] ExportToTxt(OcrJob job)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tài liệu: {job.OriginalFileName}");
        sb.AppendLine($"Thời gian OCR: {job.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        if (job.AverageConfidence.HasValue)
        {
            sb.AppendLine($"Độ tin cậy trung bình: {job.AverageConfidence.Value:F1}%");
        }
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        if (job.Pages.Count > 0)
        {
            var orderedPages = job.Pages.OrderBy(p => p.PageNumber).ToList();
            foreach (var page in orderedPages)
            {
                if (orderedPages.Count > 1)
                {
                    sb.AppendLine($"--- Trang {page.PageNumber} ---");
                    sb.AppendLine();
                }

                sb.AppendLine(page.ExtractedText ?? string.Empty);
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine(job.ExtractedText ?? string.Empty);
        }

        // Return UTF-8 with BOM for optimal compatibility with Windows text editors
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] ExportToDocx(OcrJob job)
    {
        using var stream = new MemoryStream();

        using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Add Header
            var titlePara = body.AppendChild(new Paragraph());
            var titleRun = titlePara.AppendChild(new Run());
            titleRun.AppendChild(new Text($"Kết quả OCR: {job.OriginalFileName}"));
            var titleProps = new RunProperties(new Bold(), new FontSize { Val = "32" }); // 16pt
            titleRun.PrependChild(titleProps);

            var metaPara = body.AppendChild(new Paragraph());
            var metaRun = metaPara.AppendChild(new Run());
            var confText = job.AverageConfidence.HasValue ? $" | Độ tin cậy: {job.AverageConfidence.Value:F1}%" : "";
            metaRun.AppendChild(new Text($"Ngày xử lý: {job.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC{confText}"));
            var metaProps = new RunProperties(new Italic(), new Color { Val = "666666" }, new FontSize { Val = "20" }); // 10pt
            metaRun.PrependChild(metaProps);

            // Divider
            body.AppendChild(new Paragraph());

            var orderedPages = job.Pages.OrderBy(p => p.PageNumber).ToList();
            if (orderedPages.Count > 0)
            {
                for (int i = 0; i < orderedPages.Count; i++)
                {
                    var page = orderedPages[i];

                    if (orderedPages.Count > 1)
                    {
                        var pageHeaderPara = body.AppendChild(new Paragraph());
                        var pageHeaderRun = pageHeaderPara.AppendChild(new Run());
                        pageHeaderRun.AppendChild(new Text($"--- Trang {page.PageNumber} ---"));
                        pageHeaderRun.PrependChild(new RunProperties(new Bold(), new FontSize { Val = "24" }));
                    }

                    var lines = (page.ExtractedText ?? string.Empty).Split('\n');
                    foreach (var line in lines)
                    {
                        var cleanLine = line.TrimEnd('\r');
                        var para = body.AppendChild(new Paragraph());
                        var run = para.AppendChild(new Run());
                        run.AppendChild(new Text(cleanLine));
                        run.PrependChild(new RunProperties(new FontSize { Val = "22" })); // 11pt
                    }

                    // Add Page Break if not the last page
                    if (i < orderedPages.Count - 1)
                    {
                        var breakPara = body.AppendChild(new Paragraph());
                        var breakRun = breakPara.AppendChild(new Run());
                        breakRun.AppendChild(new Break { Type = BreakValues.Page });
                    }
                }
            }
            else if (!string.IsNullOrEmpty(job.ExtractedText))
            {
                var lines = job.ExtractedText.Split('\n');
                foreach (var line in lines)
                {
                    var cleanLine = line.TrimEnd('\r');
                    var para = body.AppendChild(new Paragraph());
                    var run = para.AppendChild(new Run());
                    run.AppendChild(new Text(cleanLine));
                    run.PrependChild(new RunProperties(new FontSize { Val = "22" }));
                }
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    public byte[] ExportToPdf(OcrJob job)
    {
        using var pdfDoc = new PdfDocument();
        pdfDoc.Info.Title = $"OCR - {job.OriginalFileName}";
        pdfDoc.Info.Author = "OCR Document Recognition Web App";

        var fontTitle = new XFont("Arial", 14, XFontStyle.Bold);
        var fontPageHeader = new XFont("Arial", 12, XFontStyle.Bold);
        var fontBody = new XFont("Arial", 10, XFontStyle.Regular);
        var fontMeta = new XFont("Arial", 9, XFontStyle.Italic);

        var orderedPages = job.Pages.OrderBy(p => p.PageNumber).ToList();

        if (orderedPages.Count == 0)
        {
            // Single page fallback
            var page = pdfDoc.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            DrawPageText(gfx, page, 1, 1, job.OriginalFileName, job.ExtractedText ?? string.Empty, fontTitle, fontPageHeader, fontBody, fontMeta);
        }
        else
        {
            for (int i = 0; i < orderedPages.Count; i++)
            {
                var jobPage = orderedPages[i];
                var page = pdfDoc.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);
                DrawPageText(gfx, page, jobPage.PageNumber, orderedPages.Count, job.OriginalFileName, jobPage.ExtractedText ?? string.Empty, fontTitle, fontPageHeader, fontBody, fontMeta);
            }
        }

        using var ms = new MemoryStream();
        pdfDoc.Save(ms, false);
        return ms.ToArray();
    }

    private static void DrawPageText(
        XGraphics gfx,
        PdfPage page,
        int pageNumber,
        int totalPages,
        string fileName,
        string text,
        XFont fontTitle,
        XFont fontPageHeader,
        XFont fontBody,
        XFont fontMeta)
    {
        double margin = 40;
        double y = margin;
        double lineHeight = 16;
        double maxWidth = page.Width - 2 * margin;

        // Draw Header
        if (pageNumber == 1)
        {
            gfx.DrawString($"OCR: {fileName}", fontTitle, XBrushes.DarkSlateBlue, new XPoint(margin, y));
            y += 20;
            gfx.DrawString($"Trang {pageNumber} / {totalPages}", fontMeta, XBrushes.Gray, new XPoint(margin, y));
            y += 20;
            gfx.DrawLine(XPens.LightGray, margin, y, page.Width - margin, y);
            y += 15;
        }
        else
        {
            gfx.DrawString($"Trang {pageNumber} / {totalPages}", fontPageHeader, XBrushes.DarkSlateBlue, new XPoint(margin, y));
            y += 18;
            gfx.DrawLine(XPens.LightGray, margin, y, page.Width - margin, y);
            y += 15;
        }

        // Draw Lines
        var lines = text.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                y += lineHeight;
                continue;
            }

            // Word wrap line if longer than maxWidth
            var words = line.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                var size = gfx.MeasureString(testLine, fontBody);

                if (size.Width > maxWidth && currentLine.Length > 0)
                {
                    if (y + lineHeight > page.Height - margin)
                    {
                        break; // Stop if exceeding single page boundary
                    }
                    gfx.DrawString(currentLine.ToString(), fontBody, XBrushes.Black, new XPoint(margin, y));
                    y += lineHeight;
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    currentLine.Clear();
                    currentLine.Append(testLine);
                }
            }

            if (currentLine.Length > 0 && y + lineHeight <= page.Height - margin)
            {
                gfx.DrawString(currentLine.ToString(), fontBody, XBrushes.Black, new XPoint(margin, y));
                y += lineHeight;
            }

            if (y > page.Height - margin)
            {
                break;
            }
        }
    }
}
