namespace Jobs.Application;

public sealed record AiAnalysisRequest(
    Guid JobId,
    string JobType,
    string Payload);
