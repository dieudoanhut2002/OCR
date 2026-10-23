using OcrWebApp.Models.Entities;

namespace OcrWebApp.Models.ViewModels;

public class HomeViewModel
{
    public string DefaultLanguage { get; set; } = "vie";
    public List<OcrJob> RecentJobs { get; set; } = new();
}
