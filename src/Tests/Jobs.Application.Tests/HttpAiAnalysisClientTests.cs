namespace Jobs.Application.Tests;

using System.Net;
using System.Text;
using System.Text.Json;
using Jobs.Infrastructure.AI;

public sealed class HttpAiAnalysisClientTests
{
    [Fact]
    public async Task AnalyzeAsync_SendsSnakeCaseContractAndMapsResult()
    {
        var id = Guid.NewGuid();
        using var handler = new ResponseHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://ai-service:8000/v1/analyze", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(id.ToString(), body.RootElement.GetProperty("job_id").GetString());
            Assert.Equal("analysis", body.RootElement.GetProperty("job_type").GetString());
            Assert.Equal("text", body.RootElement.GetProperty("payload").GetString());
            return JsonResponse(id, "Summary.", "stub-analyzer-v1");
        });
        using var http = new HttpClient(handler);
        var client = new HttpAiAnalysisClient(http, new AiServiceOptions { BaseUrl = "http://ai-service:8000" });

        var result = await client.AnalyzeAsync(new AiAnalysisRequest(id, "analysis", "text"), CancellationToken.None);

        Assert.Equal("Summary.", result.Summary);
        Assert.Equal("stub-analyzer-v1", result.Model);
    }

    [Theory]
    [InlineData("", "stub")]
    [InlineData(" ", "stub")]
    [InlineData("summary", "")]
    public async Task AnalyzeAsync_WhenResponseIsIncomplete_Throws(string summary, string model)
    {
        var id = Guid.NewGuid();
        using var handler = new ResponseHandler(_ => Task.FromResult(JsonResponse(id, summary, model)));
        using var http = new HttpClient(handler);
        var client = new HttpAiAnalysisClient(http, new AiServiceOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.AnalyzeAsync(new AiAnalysisRequest(id, "analysis", "text"), CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseBelongsToAnotherJob_Throws()
    {
        using var handler = new ResponseHandler(_ => Task.FromResult(JsonResponse(Guid.NewGuid(), "Summary.", "stub")));
        using var http = new HttpClient(handler);
        var client = new HttpAiAnalysisClient(http, new AiServiceOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.AnalyzeAsync(new AiAnalysisRequest(Guid.NewGuid(), "analysis", "text"), CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenServiceReturnsError_ThrowsHttpFailure()
    {
        using var handler = new ResponseHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var http = new HttpClient(handler);
        var client = new HttpAiAnalysisClient(http, new AiServiceOptions());

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.AnalyzeAsync(new AiAnalysisRequest(Guid.NewGuid(), "analysis", "text"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
    }

    private static HttpResponseMessage JsonResponse(Guid id, string summary, string model) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { job_id = id, summary, model, latency_ms = 10 }), Encoding.UTF8, "application/json")
    };

    private sealed class ResponseHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
