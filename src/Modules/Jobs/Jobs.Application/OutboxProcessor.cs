namespace Jobs.Application;

using System.Text.Json;
using Jobs.Contracts;

public sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly IOutboxStore _outboxStore;
    private readonly IJobQueuePublisher _jobQueuePublisher;
    private readonly OutboxOptions _options;

    public OutboxProcessor(
        IOutboxStore outboxStore,
        IJobQueuePublisher jobQueuePublisher,
        OutboxOptions options)
    {
        _outboxStore = outboxStore;
        _jobQueuePublisher = jobQueuePublisher;
        _options = options;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var items = await _outboxStore.GetUnprocessedAsync(_options.BatchSize, cancellationToken);

        var published = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var message = JsonSerializer.Deserialize<JobSubmittedMessage>(
                    item.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (message is null)
                {
                    await _outboxStore.MarkFailedAsync(
                        item.Id,
                        "Outbox payload deserialized to null.",
                        cancellationToken);
                    continue;
                }

                await _jobQueuePublisher.PublishJobSubmittedAsync(message, cancellationToken);
                await _outboxStore.MarkProcessedAsync(item.Id, cancellationToken);
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await _outboxStore.MarkFailedAsync(item.Id, exception.Message, cancellationToken);
            }
        }

        return published;
    }
}
