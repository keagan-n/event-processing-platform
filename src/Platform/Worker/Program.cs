using DotNetEnv;
using Jobs.Application;
using Jobs.Infrastructure.AI;
using Jobs.Infrastructure.Persistence;
using Jobs.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Platform.Worker;
using Polly;
using Serilog;
using Serilog.Formatting.Compact;

Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

// Structured JSON console logging
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

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

var rabbitMqOptions = new RabbitMqOptions
{
    Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
    Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port : 5672,
    Username = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest",
    QueueName = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE_NAME") ?? "jobs.submitted",
    DeadLetterQueueName = Environment.GetEnvironmentVariable("RABBITMQ_DLQ_NAME") ?? "jobs.submitted.dlq",
    PrefetchCount = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PREFETCH"), out var prefetch) ? prefetch : (ushort)4
};

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddScoped<IJobQueuePublisher, RabbitMqJobQueuePublisher>();

var jobProcessingOptions = new JobProcessingOptions
{
    MaxAttempts = int.TryParse(Environment.GetEnvironmentVariable("JOB_MAX_ATTEMPTS"), out var maxAttempts) ? maxAttempts : 3
};

builder.Services.AddSingleton(jobProcessingOptions);

var aiServiceOptions = new AiServiceOptions
{
    BaseUrl = Environment.GetEnvironmentVariable("AI_SERVICE_BASE_URL") ?? "http://localhost:8000"
};

builder.Services.AddSingleton(aiServiceOptions);
builder.Services.AddHttpClient<IAiAnalysisClient, HttpAiAnalysisClient>()
    // Layer 1 resilience: absorbs transient AI-service blips (5xx, timeouts,
    // connection failures) in-process. Sustained failure surfaces to the
    // message-level retry + DLQ flow in JobProcessingService/the consumer.
    .AddResilienceHandler("ai-analysis-retry", resilience =>
    {
        resilience.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1)
        });
    });

builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobProcessingService, JobProcessingService>();
builder.Services.AddHostedService<JobSubmittedMessageConsumerService>();

var host = builder.Build();
host.Run();
