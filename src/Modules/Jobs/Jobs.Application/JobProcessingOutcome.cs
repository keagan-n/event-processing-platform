namespace Jobs.Application;

public enum JobProcessingOutcome
{
    Completed,
    Skipped,
    RetryScheduled,
    FailedPermanently
}
