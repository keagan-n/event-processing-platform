using Jobs.Contracts;

namespace Jobs.Application;

public interface IJobQueuePublisher
{
    Task PublishJobSubmittedAsync(
        JobSubmittedMessage message,
        CancellationToken cancellationToken);

    Task PublishJobRetryAsync(
        JobSubmittedMessage message,
        CancellationToken cancellationToken);

    Task PublishJobFailedAsync(
        JobSubmittedMessage message,
        string failureReason,
        CancellationToken cancellationToken);
}
