using System.Threading.Channels;
using OcrWebApp.Services.Abstractions;

namespace OcrWebApp.Services.Implementations;

public class BackgroundOcrQueue : IBackgroundOcrQueue
{
    private readonly Channel<Guid> _channel;

    public BackgroundOcrQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Guid>(options);
    }

    public async ValueTask QueueJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async ValueTask<Guid> DequeueJobAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
