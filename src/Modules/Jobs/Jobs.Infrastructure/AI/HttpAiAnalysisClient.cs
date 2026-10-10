using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Jobs.Application;

namespace Jobs.Infrastructure.AI;

public sealed class HttpAiAnalysisClient : IAiAnalysisClient
{
    private readonly HttpClient _httpClient;

    public HttpAiAnalysisClient(HttpClient httpClient, AiServiceOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _httpClient.BaseAddress = new Uri(options.BaseUrl);
    }

    public async Task<AiAnalysisResult> AnalyzeAsync(
        AiAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var httpRequest = new AnalyzeHttpRequest(
            JobId: request.JobId.ToString(),
            JobType: request.JobType,
            Payload: request.Payload);

        using var response = await _httpClient.PostAsJsonAsync(
            "/v1/analyze",
            httpRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var httpResponse = await response.Content.ReadFromJsonAsync<AnalyzeHttpResponse>(
            cancellationToken);

        if (httpResponse is null)
        {
            throw new InvalidOperationException("AI service returned an empty analyze response.");
        }

        if (!Guid.TryParse(httpResponse.JobId, out var responseJobId) || responseJobId != request.JobId)
        {
            throw new InvalidOperationException("AI service returned a response for a different job.");
        }

        if (string.IsNullOrWhiteSpace(httpResponse.Summary) || string.IsNullOrWhiteSpace(httpResponse.Model))
        {
            throw new InvalidOperationException("AI service returned an incomplete analyze response.");
        }

        return new AiAnalysisResult(
            Summary: httpResponse.Summary,
            Model: httpResponse.Model);
    }

    private sealed record AnalyzeHttpRequest(
        [property: JsonPropertyName("job_id")] string JobId,
        [property: JsonPropertyName("job_type")] string JobType,
        [property: JsonPropertyName("payload")] string Payload);

    private sealed record AnalyzeHttpResponse(
        [property: JsonPropertyName("job_id")] string JobId,
        [property: JsonPropertyName("summary")] string Summary,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("latency_ms")] int LatencyMs);
}
