namespace Jobs.Application;

public sealed class OutboxOptions
{
    public int PollIntervalSeconds { get; init; } = 2;
    public int BatchSize { get; init; } = 20;
}
