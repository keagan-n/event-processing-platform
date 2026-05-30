# AGENTS.md

This repository is a C#/.NET modular monolith for a simplified job-processing platform.

Agents working in this repo must follow the MVP specs in:

```text
.github/specs/
```

The current MVP is:

```text
POST job -> persist in PostgreSQL -> worker processes pending jobs -> status updates -> GET job status
```

## Current Stack

- C#/.NET
- PostgreSQL through Docker Compose
- EF Core
- Npgsql
- DotNetEnv for local .env loading
- Postman for API testing
- DBeaver for database inspection

## Current Working Features

- `GET /health`
- `POST /jobs`
- `GET /jobs/{jobId}/status`
- PostgreSQL persistence
- EF Core migrations
- string `TenantId`
- Guid `JobId`
- `Job` domain lifecycle methods:
  - `MarkProcessing()`
  - `MarkCompleted()`
  - `MarkFailed(reason)`

## Current Next Milestone

Implement `Platform.Worker` processing.

The Worker should:

1. Poll for pending jobs.
2. Call a Jobs.Application service.
3. Mark job `Processing`.
4. Simulate work.
5. Mark job `Completed`.
6. Mark job `Failed` if processing throws.

## Scope Rules

Do not add these unless explicitly requested:

- Redis
- rate limiting
- Python AI service calls
- queues
- outbox relay
- retries
- dead-letter queue
- auth
- distributed tracing
- k6 tests
- advanced multi-tenancy

## Architecture Rules

- `Platform.API` handles HTTP only.
- `Jobs.Application` handles use cases.
- `Jobs.Domain` handles invariants and status transitions.
- `Jobs.Infrastructure` handles EF Core/PostgreSQL.
- `Platform.Worker` hosts background processing.
- Domain must not reference EF Core, ASP.NET, PostgreSQL, Redis, HTTP, or DI.
- API endpoints should not use `JobsDbContext` directly.
- Worker should not mutate database rows directly.
- Worker should call application services.
- Application services should use repository abstractions.

## Build and Run Commands

From repo root:

```bash
docker compose up -d
dotnet build src/Platform.sln
dotnet run --project src/Platform/API/Platform.Api.csproj
```

Run migrations:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Modules/Jobs/Jobs.Infrastructure/Jobs.Infrastructure.csproj \
  --startup-project src/Platform/API/Platform.Api.csproj \
  --context JobsDbContext \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Modules/Jobs/Jobs.Infrastructure/Jobs.Infrastructure.csproj \
  --startup-project src/Platform/API/Platform.Api.csproj \
  --context JobsDbContext
```

## Local Secrets

The repo uses `.env` for local development.

Do not commit:

```text
.env
```

Commit:

```text
.env.example
```

The API loads `.env` using:

```csharp
Env.TraversePath().Load();
```

## Testing

Manual Postman tests:

```text
GET  http://localhost:5200/health
POST http://localhost:5200/jobs
GET  http://localhost:5200/jobs/{jobId}/status
```

Example job request:

```json
{
  "tenantId": "tenant-demo",
  "jobType": "ai.summary",
  "payload": "{\"text\":\"Summarize this content\"}"
}
```

## Agent Behavior

Before editing:

1. Inspect relevant files.
2. List exact files to change.
3. Explain the plan.
4. Keep the change small.

After editing:

1. Summarize files changed.
2. Explain why.
3. Provide build command.
4. Provide test steps.
