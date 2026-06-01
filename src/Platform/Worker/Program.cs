using DotNetEnv;
using Jobs.Application;
using Jobs.Infrastructure.Persistence;
using Jobs.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Platform.Worker;

Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

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
    QueueName = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE_NAME") ?? "jobs.submitted"
};

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobProcessingService, JobProcessingService>();
builder.Services.AddHostedService<JobSubmittedMessageConsumerService>();

var host = builder.Build();
host.Run();
