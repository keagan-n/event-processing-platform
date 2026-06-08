# Architecture Rules (Working Memory)

Hard constraints. See `.github/specs/architecture.md` for full architecture spec.

## Layer Responsibilities

- `Platform.API` — HTTP boundary only. Validates, maps to commands, returns responses. Publishes queue messages after persistence.
- `Jobs.Application` — Use-case orchestration. Owns `IJobRepository`, `IJobQueuePublisher`, `IJobProcessingService`, `IJobSubmissionService`, `IJobStatusService`.
- `Jobs.Domain` — Business rules and lifecycle. Owns `Job` entity, `JobStatus` enum, `MarkProcessing()`, `MarkCompleted()`, `MarkFailed()`.
- `Jobs.Infrastructure` — EF Core/PostgreSQL persistence and RabbitMQ publisher implementation.
- `Platform.Worker` — Consumes `JobSubmittedMessage` from RabbitMQ. Resolves `IJobProcessingService` per message.

## Hard Rules

- Worker must not poll PostgreSQL. Worker loads jobs by `jobId` from queue messages.
- Domain must not reference EF Core, ASP.NET, PostgreSQL, RabbitMQ, or HTTP.
- API must not use `JobsDbContext` directly.
- Worker must not mutate status fields directly — goes through domain methods.
- No Redis.
- No OutboxRelay or outbox pattern.
- No direct OpenAI SDK from .NET. Future AI goes through Python `ai-service`.
- PostgreSQL is the system of record.
- DB save + RabbitMQ publish is intentionally non-atomic (outbox out of scope).
