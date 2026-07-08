using DotNetEnv;
using Jobs.Application;
using Jobs.Infrastructure.Persistence;
using Jobs.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Platform.Api;
using Platform.Api.Contracts.Jobs;
using Platform.Api.RateLimiting;
using Serilog;
using Serilog.Formatting.Compact;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Structured JSON console logging
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Register framework services
builder.Services.AddOpenApi();

// Register database
builder.Services.AddDbContext<JobsDbContext>(options =>
{
    var connectionString = Environment.GetEnvironmentVariable("JOBS_DATABASE_CONNECTION_STRING");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "JOBS_DATABASE_CONNECTION_STRING environment variable is not set.");
    }

    options.UseNpgsql(connectionString);
});
// Register application services
builder.Services.AddScoped<IJobSubmissionService, JobSubmissionService>();
builder.Services.AddScoped<IJobStatusService, JobStatusService>();

// Register infrastructure services
builder.Services.AddScoped<IJobRepository, JobRepository>();

var rabbitMqPort = 5672;
var rabbitMqPortRaw = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
if (!string.IsNullOrWhiteSpace(rabbitMqPortRaw) &&
    int.TryParse(rabbitMqPortRaw, out var parsedPort))
{
    rabbitMqPort = parsedPort;
}

var rabbitMqOptions = new RabbitMqOptions
{
    Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
    Port = rabbitMqPort,
    Username = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest",
    QueueName = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE_NAME") ?? "jobs.submitted"
};

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddScoped<IJobQueuePublisher, RabbitMqJobQueuePublisher>();

// Transactional outbox: submission stages messages in the jobs transaction;
// the dispatcher publishes them to RabbitMQ out-of-band.
var outboxOptions = new OutboxOptions
{
    PollIntervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("OUTBOX_POLL_SECONDS"), out var pollSeconds) ? pollSeconds : 2
};

builder.Services.AddSingleton(outboxOptions);
builder.Services.AddScoped<IOutboxWriter, EfOutboxWriter>();
builder.Services.AddScoped<IOutboxStore, EfOutboxStore>();
builder.Services.AddScoped<IOutboxProcessor, OutboxProcessor>();
builder.Services.AddHostedService<OutboxDispatcherService>();

// Per-tenant ingestion rate limiting (in-memory token bucket)
var rateLimitTokens = int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_TOKENS"), out var tokens) ? tokens : 10;
var rateLimitWindowSeconds = int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_WINDOW_SECONDS"), out var windowSeconds) ? windowSeconds : 60;

builder.Services.AddSingleton(new TenantRateLimiter(rateLimitTokens, rateLimitWindowSeconds));

var app = builder.Build();

// Containerized runs apply EF migrations on startup; local dev stays manual
// (dotnet ef database update).
if (string.Equals(
        Environment.GetEnvironmentVariable("APPLY_MIGRATIONS"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
    dbContext.Database.Migrate();
}

app.UseSerilogRequestLogging();

// Configure development-only tooling
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Simple health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

// Submit a new job
app.MapPost("/jobs", async (
    SubmitJobRequest request,
    IJobSubmissionService jobSubmissionService,
    TenantRateLimiter tenantRateLimiter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    // Rate-limit per tenant before doing any work. Blank TenantId skips the
    // limiter — validation below rejects it with 400 anyway.
    var tenantId = request.TenantId?.Trim();

    if (!string.IsNullOrWhiteSpace(tenantId) &&
        !await tenantRateLimiter.TryAcquireAsync(tenantId, cancellationToken))
    {
        httpContext.Response.Headers.RetryAfter = tenantRateLimiter.WindowSeconds.ToString();

        return Results.Json(
            new { message = "Rate limit exceeded.", tenantId },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    var command = new SubmitJobCommand(
        TenantId: request.TenantId,
        JobType: request.JobType,
        Payload: request.Payload);

    var result = await jobSubmissionService.SubmitAsync(command, cancellationToken);

    if (!result.Succeeded)
    {
        return Results.BadRequest(new
        {
            message = "Validation failed.",
            errors = result.Errors
        });
    }

    var response = new SubmitJobResponse(
        JobId: result.JobId,
        TenantId: result.TenantId,
        JobType: result.JobType,
        Status: result.Status,
        CreatedUtc: result.CreatedUtc);

    return Results.Created($"/jobs/{response.JobId}", response);
})
.WithName("SubmitJob");

// Get job status
app.MapGet("/jobs/{jobId:guid}/status", async (
    Guid jobId,
    IJobStatusService jobStatusService,
    CancellationToken cancellationToken) =>
{
    var result = await jobStatusService.GetStatusAsync(jobId, cancellationToken);

    if (result is null)
    {
        return Results.NotFound(new
        {
            message = "Job not found.",
            jobId
        });
    }

    var response = new GetJobStatusResponse(
        JobId: result.JobId,
        TenantId: result.TenantId,
        JobType: result.JobType,
        Status: result.Status,
        CreatedUtc: result.CreatedUtc,
        Result: result.Result,
        AttemptCount: result.AttemptCount);

    return Results.Ok(response);
})
.WithName("GetJobStatus");

app.Run();
