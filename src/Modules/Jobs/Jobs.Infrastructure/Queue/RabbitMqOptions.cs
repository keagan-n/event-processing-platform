namespace Jobs.Infrastructure.Queue;

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string QueueName { get; init; } = "jobs.submitted";
    public string DeadLetterQueueName { get; init; } = "jobs.submitted.dlq";
    public ushort PrefetchCount { get; init; } = 4;
}
