# CLAUDE.md

This file gives Claude Code project-specific guidance.

## Project

This repo is a simplified event/job-processing platform using:

- C#/.NET
- PostgreSQL
- EF Core
- Docker Compose
- Modular monolith architecture

## Current Flow

```text
POST /jobs -> rate limit -> persist Job + outbox row in one transaction
Outbox dispatcher (API host) -> publishes JobSubmittedMessage to RabbitMQ
Worker consumes RabbitMQ -> processes through Jobs.Application -> calls Python ai-service
Failures retry up to JOB_MAX_ATTEMPTS then dead-letter to jobs.submitted.dlq
GET /jobs/{jobId}/status -> status, result, attemptCount
```

PostgreSQL is the system of record. RabbitMQ is the active queue. Worker does not poll PostgreSQL.

## Current State

See `.github/specs/current-state.md` for full implemented behavior.
See `.github/specs/completion-plan.md` for the authoritative final-scope plan and milestone order.
See `.claude/context/README.md` for Claude Code working memory.

Already implemented (all completion-plan milestones, 1-6):

- `GET /health`, `POST /jobs`, `GET /jobs/{jobId}/status` (status + result + attemptCount)
- PostgreSQL persistence via EF Core (5 migrations, incl. `outbox_messages`)
- Transactional outbox: `IOutboxWriter`/`EfOutboxWriter` staged in the jobs transaction; `OutboxDispatcherService` in the API host publishes to RabbitMQ
- Worker consumes RabbitMQ (`JobSubmittedMessageConsumerService`, prefetch-bounded)
- Python `ai-service` (FastAPI): stub provider default, optional Anthropic provider
- Retries: resilience handler (exponential backoff + jitter) on the AI HTTP client; message-level retry via `MarkPendingForRetry`/`AttemptCount` up to `JOB_MAX_ATTEMPTS`, then `jobs.submitted.dlq`
- Per-tenant token-bucket rate limiting on `POST /jobs` (429 + Retry-After)
- Job lifecycle: `Pending -> Processing -> (Completed | Failed | Pending-for-retry)`
- `forceFailure` payload exercises the retry-then-DLQ path
- 39 xUnit tests + 12 pytest tests; GitHub Actions CI
- Serilog structured JSON logging; full-stack `docker compose up --build`

Next milestone:

```text
None — completion-plan.md milestones 1-6 are done and verified end-to-end.
Remaining ideas live in README "Known Limitations / Future Work".
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

Do not implement (permanently out of scope — see completion-plan.md for rationale):

- Redis
- outbox relay (as a separate deployable; the in-process outbox dispatcher IS in scope)
- auth
- k6 tests
- cloud deployment
- Kubernetes
- frontend/UI

Now IN scope as planned milestones (see `.github/specs/completion-plan.md`):

- dead-letter queue (Milestone 3)
- transactional outbox with in-process dispatcher (Milestone 5)

Roadmap constraints:

- Queue-based dispatch is complete.
- Milestone order in completion-plan.md is authoritative.
- Rate limiting should not be Redis-backed unless explicitly redesigned.
- AI calls go through the Python `ai-service`, never a direct LLM SDK call from .NET.

Outbox tradeoff note (RESOLVED):

- Historically, DB save + queue publish was not atomic and a publish failure could lose dispatch.
- Milestone 5 closed this: job + outbox row commit in one transaction; the in-process dispatcher publishes out-of-band.
- Delivery is at-least-once; the consumer's non-Pending skip guard makes processing idempotent.

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
