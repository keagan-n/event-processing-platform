namespace Jobs.Application;

public sealed class JobProcessingOptions
{
    public int MaxAttempts { get; init; } = 3;
}
