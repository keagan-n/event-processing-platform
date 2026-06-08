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
POST /jobs -> persist in PostgreSQL -> publish JobSubmittedMessage to RabbitMQ
Worker consumes RabbitMQ -> loads job by jobId -> processes through Jobs.Application -> GET job status
```

PostgreSQL is the system of record. RabbitMQ is the active queue. Worker does not poll PostgreSQL.

## Current State

See `.github/specs/current-state.md` for full implemented behavior.
See `.github/specs/queue-ai-retry-rate-limit-roadmap.md` for active roadmap.
See `.claude/context/README.md` for Claude Code working memory.

Already implemented:

- `GET /health`
- `POST /jobs` (validates, persists, publishes to RabbitMQ)
- `GET /jobs/{jobId}/status`
- PostgreSQL persistence via EF Core
- RabbitMQ queue publishing (`IJobQueuePublisher`, `RabbitMqJobQueuePublisher`)
- `JobSubmittedMessage` contract
- Worker consumes RabbitMQ messages (`JobSubmittedMessageConsumerService`)
- Worker processes jobs through `IJobProcessingService`
- Job lifecycle: `Pending -> Processing -> Completed` or `Pending -> Processing -> Failed`
- `forceFailure` payload triggers failure path

Next milestone:

```text
Queue-based dispatch is complete.
Next: Python AI service integration, then retries, then rate limiting.
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

- Queue-based dispatch is complete.
- Python AI service integration is next.
- Retry policies come after AI integration.
- Rate limiting remains in roadmap and comes later.
- Rate limiting should not be Redis-backed unless explicitly redesigned.
- Future AI calls go through Python `ai-service`, not direct OpenAI SDK from .NET.

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

## Agent Rules

- Do not commit or push unless explicitly told.
- Inspect relevant files before editing.
- Show `git diff --stat` and `git diff` before any commit.
- Run `dotnet build src/Platform.sln` after code changes.
- Use conventional commit messages: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`, `test:`.
- Do not delete or rename files without explicit approval.
- If `.claude/context/` conflicts with `.github/specs/`, `.github/specs/` wins.

See `.claude/context/README.md` for Claude Code working memory layout.
See `.github/specs/index.md` for documentation map.

## Common Commands

```bash
docker compose up -d
dotnet build src/Platform.sln
dotnet run --project src/Platform/API/Platform.Api.csproj
dotnet run --project src/Platform/Worker/Platform.Worker.csproj
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
