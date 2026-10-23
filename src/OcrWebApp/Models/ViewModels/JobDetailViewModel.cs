using OcrWebApp.Models.Entities;

namespace OcrWebApp.Models.ViewModels;

public class JobDetailViewModel
{
    public OcrJob Job { get; set; } = null!;
    public int CurrentPageNumber { get; set; } = 1;
    public OcrJobPage? CurrentPage => Job.Pages.FirstOrDefault(p => p.PageNumber == CurrentPageNumber);
    public bool HasMultiplePages => Job.TotalPages > 1;
}
