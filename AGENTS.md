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
- RabbitMQ through Docker Compose
- EF Core
- Npgsql
- DotNetEnv for local .env loading
- Postman for API testing
- DBeaver for database inspection

## Current Working Features

- `GET /health`
- `POST /jobs` (validates, persists, publishes `JobSubmittedMessage` to RabbitMQ)
- `GET /jobs/{jobId}/status`
- PostgreSQL persistence via EF Core
- RabbitMQ queue publishing (`RabbitMqJobQueuePublisher`)
- Worker consumes RabbitMQ messages (`JobSubmittedMessageConsumerService`)
- Worker processes jobs through `IJobProcessingService`
- string `TenantId`
- Guid `JobId`
- `Job` domain lifecycle methods:
  - `MarkProcessing()`
  - `MarkCompleted()`
  - `MarkFailed(reason)`

## Current Next Milestone

Queue-based dispatch is complete. Worker consumes RabbitMQ messages via `JobSubmittedMessageConsumerService`. Worker does not poll PostgreSQL.

Next active implementation order:

1. Python AI service integration.
2. Retries after AI integration.
3. Rate limiting after queue + AI + retries.

## Scope Rules

Do not add these unless explicitly requested:

- Redis
- outbox relay
- outbox pattern
- rate limiting
- dead-letter queue
- auth
- distributed tracing
- k6 tests
- advanced multi-tenancy

Roadmap note:

- Queue-based dispatch and Python AI service integration are active planned milestones.
- Retries come after AI integration.
- Rate limiting remains in roadmap but comes later and should not be Redis-backed unless explicitly redesigned.

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

Queue/outbox tradeoff to keep explicit:

- DB save + queue publish is simpler but non-atomic.
- Publish failure after DB save can lose dispatch.
- Outbox/transactional messaging solves this in production but is intentionally out of active scope for this portfolio MVP.

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

## Cross-Agent Rules

- Do not commit or push unless explicitly requested.
- Show `git diff --stat` and `git diff` before any commit.
- Use semantic commit messages: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`, `test:`.
- No Redis unless explicitly redesigned.
- No OutboxRelay or outbox pattern.
- RabbitMQ is the active queue. Worker consumes RabbitMQ and must not poll PostgreSQL.
- Python AI service is the next integration target. No direct OpenAI SDK calls from .NET.
- Do not delete or rename files without explicit approval.
- No broad cleanup without grep/reference checks.
- If runtime behavior changes, update `.github/specs/current-state.md`.

See `.github/specs/index.md` for documentation map.
See `.github/specs/agent-operating-model.md` for full agent operating rules.
