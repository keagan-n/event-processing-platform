using Jobs.Contracts;

namespace Jobs.Application;

public interface IJobQueuePublisher
{
    Task PublishJobSubmittedAsync(
        JobSubmittedMessage message,
        CancellationToken cancellationToken);
}
