# Current State (Working Memory)

Mirror of `.github/specs/current-state.md`. If conflict, the spec wins.

## Implemented Runtime Behavior

- API validates `POST /jobs` requests (tenantId, jobType, payload required).
- API persists valid jobs to PostgreSQL.
- API publishes `JobSubmittedMessage` to RabbitMQ after persistence succeeds.
- `GET /jobs/{jobId}/status` reads persisted job status from PostgreSQL.
- Worker consumes `JobSubmittedMessage` from RabbitMQ.
- Worker loads job by `jobId` from PostgreSQL.
- Worker processes Pending jobs through `IJobProcessingService` in Jobs.Application.
- Normal jobs: `Pending -> Processing -> Completed`.
- `forceFailure` payload jobs: `Pending -> Processing -> Failed`.
- PostgreSQL is the system of record.
- RabbitMQ is the active queue.
- Worker does not poll PostgreSQL.

## Removed from Active Scope

- Redis.
- OutboxRelay.
- Outbox pattern.

## Next

Python AI service integration. See `roadmap.md`.
