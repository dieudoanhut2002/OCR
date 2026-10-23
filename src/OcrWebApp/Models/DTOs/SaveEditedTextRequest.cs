using System.ComponentModel.DataAnnotations;

namespace OcrWebApp.Models.DTOs;

public class SaveEditedTextRequest
{
    [Required]
    public Guid JobId { get; set; }

    /// <summary>
    /// If null, saves to full document text. If specified, saves to that specific page.
    /// </summary>
    public int? PageNumber { get; set; }

    public string? Content { get; set; }
}
