using DotNetEnv;
using Jobs.Application;
using Jobs.Infrastructure.Persistence;
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

builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobProcessingService, JobProcessingService>();
builder.Services.AddHostedService<JobProcessingBackgroundService>();

var host = builder.Build();
host.Run();
