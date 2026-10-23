using OcrWebApp.Models.Entities;

namespace OcrWebApp.Models.ViewModels;

public class JobHistoryViewModel
{
    public List<OcrJob> Jobs { get; set; } = new();
    public int TotalJobs => Jobs.Count;
    public int CompletedJobs => Jobs.Count(j => j.Status == OcrJobStatus.Completed);
    public int ProcessingJobs => Jobs.Count(j => j.Status == OcrJobStatus.Processing || j.Status == OcrJobStatus.Pending);
    public int FailedJobs => Jobs.Count(j => j.Status == OcrJobStatus.Failed);
}
