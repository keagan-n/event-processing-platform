# CLAUDE.md

This file gives Claude Code project-specific guidance.

## Project

This repo is a simplified event/job-processing platform using:

- C#/.NET
- PostgreSQL
- EF Core
- Docker Compose
- Modular monolith architecture

## Current MVP

The current MVP flow is:

```text
POST /jobs -> save job to PostgreSQL -> worker processes pending jobs -> GET job status
```

## Current State

Already implemented:

- `GET /health`
- `POST /jobs`
- `GET /jobs/{jobId}/status`
- PostgreSQL persistence
- EF Core migrations
- `JobsDbContext`
- `JobRepository`
- `IJobRepository`
- string `TenantId`
- Guid `JobId`
- domain lifecycle methods:
  - `MarkProcessing`
  - `MarkCompleted`
  - `MarkFailed`

Next milestone:

```text
Platform.Worker polling processing is complete.
Next: queue-based dispatch, then Python AI integration, then retries, then rate limiting.
```

## Architecture Boundaries

Claude must preserve these boundaries:

- API is HTTP transport only.
- Application owns use-case orchestration.
- Domain owns business rules.
- Infrastructure owns EF Core/PostgreSQL.
- Worker calls application services.

Do not put EF Core in Domain or Application.

Do not let API endpoints use `JobsDbContext` directly.

Do not let Worker directly mutate status fields.

## Deferred Features

Do not implement unless explicitly requested:

- Redis
- outbox
- outbox relay
- dead-letter queue
- auth
- k6 tests
- cloud deployment

Roadmap constraints:

- Queue-based dispatch is the next active implementation direction.
- Python AI calls come after queue dispatch.
- Retry policies come after AI integration.
- Rate limiting remains in roadmap and comes later.
- Rate limiting should not be Redis-backed unless explicitly redesigned.

Outbox tradeoff note:

- Without outbox, DB save + queue publish is not atomic.
- Publish failure after DB save can lose dispatch.
- Outbox/transactional messaging solves this in production but is intentionally out of active scope for this portfolio MVP.

## Development Style

Prefer:

- small changes
- exact file paths
- simple code
- explicit explanations
- build after every milestone
- manual Postman verification
- DBeaver verification when database schema changes

## Before Coding

Before modifying files:

1. Inspect relevant files.
2. State the current behavior.
3. List files to change.
4. Explain why.
5. Wait if the requested change is ambiguous.

## Common Commands

```bash
docker compose up -d
dotnet build src/Platform.sln
dotnet run --project src/Platform/API/Platform.Api.csproj
```

## Migration Commands

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
