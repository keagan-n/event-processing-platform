# Job Processing Platform — Development Guide

## Project Foundation

This is a **simplified job-processing platform** built with C#/.NET (modular monolith), PostgreSQL, and a background worker. It supports the core MVP workflow: submit jobs → persist → process → update status → read status.

**Out of scope**: Redis, AI service calls, queues, outbox pattern, retries, DLQ, authentication, rate limiting. Add only if explicitly requested.

## Architecture & Boundaries

The codebase follows clean layered architecture with clear responsibilities:

```
Jobs.Domain           # Job aggregate, JobStatus enum, business rules (no dependencies)
Jobs.Application      # Commands, handlers, services, repository abstractions
Jobs.Infrastructure   # DbContext, EF mappings, repository implementations
Platform.API          # HTTP endpoints (minimal logic)
Platform.Worker       # Background job processor
```

**Key principle**: Each layer depends inward; Domain is pristine (no EF, no HTTP, no infrastructure).

### Domain Layer (`Jobs.Domain`)
- Contains `Job` aggregate root with status transitions (Pending → Processing → Completed/Failed)
- `JobStatus` enum and any domain value objects
- Business invariants enforced in domain (e.g., only Pending jobs can start processing)
- Zero framework dependencies—pure C# logic

### Application Layer (`Jobs.Application`)
- Command/query handlers orchestrating use cases (submit, process, update status, read)
- Service abstractions (`IJobRepository`, `IJobService`) defined here
- DTO/request/response records for contracts
- No persistence logic—delegates to repository abstractions

### Infrastructure Layer (`Jobs.Infrastructure`)
- `JobsDbContext` for EF Core configuration and queries
- Entity mappings, indexes, and migrations
- `EfJobRepository` implementing `IJobRepository`
- All PostgreSQL concerns encapsulated here

### API Layer (`Platform.API`)
- HTTP endpoints forwarding to application layer
- Minimal logic; mainly translate HTTP → domain models → response
- Return appropriate status codes (200, 201, 400, 500)

### Worker (`Platform.Worker`)
- Background service polling for Pending jobs
- Executes application commands to process and update status
- Graceful shutdown on cancellation token

## Repository Structure

```
src/
├── Modules/Jobs/
│   ├── Jobs.Domain/           # Entities, enums, value objects
│   ├── Jobs.Application/       # Handlers, commands, services (abstractions)
│   └── Jobs.Infrastructure/    # DbContext, EF mappings, repository impl.
├── Platform/
│   ├── API/
│   │   ├── Program.cs
│   │   ├── Contracts/Jobs/    # Request/response DTOs
│   │   └── Endpoints/Jobs/    # HTTP routes
│   ├── Worker/
│   │   └── Program.cs         # Background service
│   └── OutboxRelay/           # (Not used for MVP)
└── Common/                     # Shared domain abstractions
```

## Code Standards

### C# / .NET Rules
See `.github/instructions/dotnet.instructions.md` for detailed patterns. Key points:

- **Async by default**: Methods handling I/O (DB, HTTP) use `async`/`await` with `CancellationToken ct`
- **Dependency injection**: Register services in `Program.cs`, inject via constructor
- **No business logic in `Program.cs`**: Keep setup minimal
- **Naming**: `SubmitJobCommand`, `ProcessJobHandler`, `IJobRepository` (I-prefix for interfaces)

### Entity & Aggregate Design
```csharp
public class Job : AggregateRoot
{
    public required Guid JobId { get; set; }
    public required Guid TenantId { get; set; }
    public required string JobType { get; set; }
    public required JobStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    
    // Domain methods enforce invariants
    public void MarkProcessing()
    {
        if (Status != JobStatus.Pending)
            throw new InvalidOperationException("Only pending jobs can transition to processing.");
        Status = JobStatus.Processing;
    }
}
```

### Repository Pattern
Define abstractions in Application; implement in Infrastructure:
```csharp
// Jobs.Application/Services/IJobRepository.cs
public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid jobId, CancellationToken ct);
    Task SaveAsync(Job job, CancellationToken ct);
    Task<IReadOnlyList<Job>> GetByStatusAsync(JobStatus status, CancellationToken ct);
}

// Jobs.Infrastructure/Persistence/EfJobRepository.cs
public class EfJobRepository : IJobRepository
{
    public async Task<Job?> GetByIdAsync(Guid jobId, CancellationToken ct)
        => await _context.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
    
    public async Task SaveAsync(Job job, CancellationToken ct)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(ct);
    }
}
```

## Workflow Examples

### Adding a New Endpoint
1. Define request/response records in `Platform.API/Contracts/Jobs/`
2. Create command/handler in `Jobs.Application/`
3. Add endpoint in `Platform.API/Endpoints/Jobs/`
4. Register handler in `Program.cs` (DI)
5. **Test**: Call endpoint, verify response and DB state

### Updating Job Status
1. Add transition method to `Job` aggregate (enforce invariant)
2. Create command + handler in Application
3. Call from Worker or API endpoint
4. Migration only needed if schema changes
5. **Test**: Verify job transitions correctly; reject invalid transitions

### Adding a Database Field
1. Add property to `Job` entity in Domain
2. Configure in `JobsDbContext.OnModelCreating()` (Infrastructure)
3. Add migration: `dotnet ef migrations add <MigrationName> -p src/Modules/Jobs/Jobs.Infrastructure -s src/Platform/API`
4. Apply: `dotnet ef database update -p src/Modules/Jobs/Jobs.Infrastructure -s src/Platform/API`
5. Update Application layer as needed
6. **Test**: Verify field persists and queries work

## Local Development (macOS)

**Prerequisites**:
```bash
dotnet --version       # .NET 8+
brew services start postgresql-15
brew services start redis-server  # If needed later
```

**Build & Test**:
```bash
dotnet build --configuration Debug
dotnet test
```

**Run Locally**:
```bash
# Terminal 1: API
dotnet run --project src/Platform/API --configuration Debug

# Terminal 2: Worker
dotnet run --project src/Platform/Worker --configuration Debug
```

**Database**:
```bash
# Apply migrations
dotnet ef database update -p src/Modules/Jobs/Jobs.Infrastructure -s src/Platform/API

# Add migration after schema change
dotnet ef migrations add AddFieldName -p src/Modules/Jobs/Jobs.Infrastructure
```

## What Copilot Should Do

✓ Generate domain entities with business invariants  
✓ Create application handlers following CQRS patterns  
✓ Implement EF Core repository methods with async/await  
✓ Add HTTP endpoints with minimal logic  
✓ Use dependency injection in Program.cs  
✓ Generate async methods with CancellationToken  
✓ Add migrations when schema changes  
✓ Explain each change: files touched, why, how to test  

✗ Do NOT add Redis, AI calls, or rate limiting without explicit request  
✗ Do NOT add retries, outbox pattern, or DLQ  
✗ Do NOT place business logic in API endpoints  
✗ Do NOT add Domain dependencies on Infrastructure or EF Core  
✗ Do NOT skip CancellationToken on async I/O methods  
✗ Do NOT rewrite large sections—make small, reviewable changes  

## References

- **Instruction files**: See `.github/instructions/dotnet.instructions.md` for C#/.NET patterns
- **Project**: Modular monolith with PostgreSQL; extensible for future modules beyond Jobs
