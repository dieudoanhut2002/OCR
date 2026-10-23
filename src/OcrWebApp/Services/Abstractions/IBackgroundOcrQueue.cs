namespace OcrWebApp.Services.Abstractions;

public interface IBackgroundOcrQueue
{
    ValueTask QueueJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueJobAsync(CancellationToken cancellationToken);
}
