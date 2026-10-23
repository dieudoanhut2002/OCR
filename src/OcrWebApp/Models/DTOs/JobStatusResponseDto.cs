namespace OcrWebApp.Models.DTOs;

public class JobStatusResponseDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int StatusNumber { get; set; }
    public int TotalPages { get; set; }
    public int ProcessedPages { get; set; }
    public float? AverageConfidence { get; set; }
    public string? ErrorMessage { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
}
