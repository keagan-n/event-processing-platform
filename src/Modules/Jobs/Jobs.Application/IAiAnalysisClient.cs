namespace Jobs.Application;

public interface IAiAnalysisClient
{
    Task<AiAnalysisResult> AnalyzeAsync(
        AiAnalysisRequest request,
        CancellationToken cancellationToken);
}
