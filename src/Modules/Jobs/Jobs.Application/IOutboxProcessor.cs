namespace Jobs.Application;

public interface IOutboxProcessor
{
    /// <summary>
    /// Publishes one batch of unprocessed outbox messages. Returns the number
    /// successfully published. Failed rows keep their error and stay
    /// unprocessed, so the next tick retries them (at-least-once delivery;
    /// the consumer's idempotency guard absorbs any duplicates).
    /// </summary>
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}
