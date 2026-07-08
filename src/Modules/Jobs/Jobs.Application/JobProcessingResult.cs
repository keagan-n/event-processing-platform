namespace Jobs.Application;

public sealed record JobProcessingResult(
    JobProcessingOutcome Outcome,
    string? FailureReason)
{
    public static JobProcessingResult Completed() =>
        new(JobProcessingOutcome.Completed, null);

    public static JobProcessingResult Skipped() =>
        new(JobProcessingOutcome.Skipped, null);

    public static JobProcessingResult RetryScheduled(string failureReason) =>
        new(JobProcessingOutcome.RetryScheduled, failureReason);

    public static JobProcessingResult FailedPermanently(string failureReason) =>
        new(JobProcessingOutcome.FailedPermanently, failureReason);
}
