---
description: "Use when writing C# code, designing .NET services, dependency injection setup, async patterns, or domain modeling. Covers DDD and Entity Framework patterns."
applyTo: "**/*.cs"
---
# C# / .NET Development

## Project Structure

```
src/
├── Domain/              # Entities, value objects, events (no dependencies)
├── Application/         # Use cases, queries, commands, handlers (business logic)
├── Infrastructure/      # EF Core, repos, external services (framework code)
└── Presentation/        # API endpoints (minimal logic)
```

## Entity Design for Multi-Tenancy

Always include `TenantId` at the aggregate root level:

```csharp
// ✓ Correct: TenantId is part of the identity
public class Event : AggregateRoot
{
    public required string TenantId { get; set; }
    public required Guid EventId { get; set; }
    public required string EventType { get; set; }
    public required DateTime OccurredAt { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
}

// ✗ Avoid: TenantId not enforced at design time
public class Event
{
    public Guid Id { get; set; }
    // Missing TenantId
}
```

## Async Patterns

Use async for I/O-bound operations (database, external calls):

```csharp
// ✓ Correct: async handler for event processing
public class ProcessEventHandler : IEventHandler<DomainEvent>
{
    public async Task HandleAsync(DomainEvent @event, CancellationToken ct)
    {
        var result = await _processor.ProcessAsync(@event, ct);
        await _repository.SaveAsync(result, ct);
    }
}

// ✗ Avoid: blocking calls or missing tenant context
public void Handle(DomainEvent @event)
{
    _processor.Process(@event); // Blocks thread
}
```

## Dependency Injection (DI) Setup

Use Microsoft.Extensions.DependencyInjection. Register services in layers:

```csharp
// Startup / Program.cs
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    // Domain services (no external dependencies)
    services.AddScoped<EventAggregateFactory>();

    // Application services (use cases)
    services.AddScoped<ProcessEventUseCase>();

    // Infrastructure (EF, external APIs)
    services.AddScoped<IEventRepository, EfEventRepository>();

    return services;
}
```

## Error Handling

Use custom exceptions for domain logic; let infrastructure exceptions bubble with context:

```csharp
// ✓ Custom exception for domain rules
public class QuotaExceededException : DomainException
{
    public string TenantId { get; }
    public QuotaExceededException(string tenantId)
        : base($"Quota exceeded for tenant {tenantId}") => TenantId = tenantId;
}

// ✓ Catch and re-throw with context
catch (DbUpdateException ex)
{
    _logger.LogError(ex, "Database error for tenant {TenantId}", tenantId);
    throw new PersistenceException($"Failed to save event for {tenantId}", ex);
}
```

## Logging

Always include tenant context and operation ID:

```csharp
_logger.LogInformation(
    "Processing event {EventId} for tenant {TenantId} (operation {OperationId})",
    @event.EventId, @event.TenantId, operationId);
```

## Testing

Unit tests should NOT touch the database. Use interfaces and mocks:

```csharp
[Test]
public async Task ProcessEvent_ExceedsQuota_ThrowsAsync()
{
    var mockLimiter = new Mock<IRateLimiter>();
    mockLimiter.Setup(x => x.CheckQuotaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var handler = new ProcessEventHandler(mockLimiter.Object, _processor, _repository);

    var ex = Assert.ThrowsAsync<QuotaExceededException>(
        () => handler.HandleAsync(_event, CancellationToken.None));
}
```

Integration tests: Spin up PostgreSQL in Docker and use test fixtures:

```csharp
[Category("Integration")]
public async Task EventPersistence_SavesWithTenantIsolationAsync()
{
    var context = new TestDbContext(_options);
    var repo = new EfEventRepository(context);

    await repo.SaveAsync(_event);

    // Query as different tenant—should return nothing
    var fetched = await repo.GetByIdAsync(_event.EventId, "other-tenant");
    Assert.IsNull(fetched);
}
```

## Naming Conventions

- **Entities**: PascalCase, singular (Event, Quota, Tenant)
- **Interfaces**: I-prefix (IRepository, IAiClient)
- **Async methods**: Async suffix (ProcessAsync, SaveAsync)
- **Services**: -Service or -UseCase suffix (ProcessEventUseCase, QuotaService)

## Performance Considerations

- **Batch operations**: Use bulk insert/update for high-volume event ingestion
- **Indexes**: Always index on (TenantId, OccurredAt) and (TenantId, where entity-specific filters)
- **Connection pooling**: EF Core handles this by default; verify pool size for concurrency
