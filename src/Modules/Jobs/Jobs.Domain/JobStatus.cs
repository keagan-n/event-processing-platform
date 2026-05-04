namespace Jobs.Domain;

public enum JobStatus
{
    Pending = 1,
    Queued = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5
}