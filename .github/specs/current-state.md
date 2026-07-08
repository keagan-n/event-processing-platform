# Current State

## Summary

All completion-plan milestones (1-6) are implemented. The platform is feature-complete per `.github/specs/completion-plan.md`.

The working flow is:

```text
Client -> Platform.API -> validate -> per-tenant rate limit check
       -> persist Job + OutboxMessage in ONE PostgreSQL transaction
OutboxDispatcherService (in API host) -> polls outbox -> publishes JobSubmittedMessage to RabbitMQ
Platform.Worker -> consumes jobs.submitted (prefetch-bounded) -> IJobProcessingService
                -> calls Python ai-service POST /v1/analyze (retry w/ exponential backoff + jitter)
                -> success: MarkCompleted(result) | failure: retry up to MaxAttempts then MarkFailed + DLQ
Client -> GET /jobs/{jobId}/status -> status, result, attemptCount
```

PostgreSQL is the system of record for all job and outbox state.

## Implemented

### API (`Platform.Api`)

- `GET /health`
- `POST /jobs` — validates (400), rate-limits per tenant (429 + Retry-After), persists job + outbox row atomically (201). No inline RabbitMQ publish.
- `GET /jobs/{jobId}/status` — returns `jobId`, `tenantId`, `jobType`, `status`, `createdUtc`, `result` (nullable), `attemptCount`.
- `TenantRateLimiter` — in-memory `PartitionedRateLimiter` token bucket per TenantId. Env: `RATE_LIMIT_TOKENS` (10), `RATE_LIMIT_WINDOW_SECONDS` (60). Applied inside the endpoint because the partition key comes from the request body.
- `OutboxDispatcherService` — BackgroundService polling every `OUTBOX_POLL_SECONDS` (2), publishing batches of 20 via `IOutboxProcessor`; failed rows keep `Error` and stay unprocessed for the next tick.
- `APPLY_MIGRATIONS=true` env applies EF migrations on startup (used by docker compose).
- Serilog structured JSON console logging + request logging.

### Domain (`Jobs.Domain`)

`Job`: `Id`, `TenantId`, `JobType`, `Payload`, `Status`, `CreatedUtc`, `StartedUtc`, `CompletedUtc`, `FailedUtc`, `FailureReason`, `Result`, `AttemptCount`.

Transitions (all others throw `InvalidOperationException`):

```text
Pending -> Processing            MarkProcessing()
Processing -> Completed          MarkCompleted(result)
Processing -> Failed             MarkFailed(reason)
Processing -> Pending (retry)    MarkPendingForRetry()  [increments AttemptCount]
```

### Application (`Jobs.Application`)

- `JobSubmissionService` — validates, trims, stages job + outbox message, single `SaveChangesAsync` (transactional outbox). Depends on `IOutboxWriter`, not the queue publisher.
- `JobProcessingService` — returns `JobProcessingResult` (`Completed` | `Skipped` | `RetryScheduled` | `FailedPermanently`). Non-Pending jobs are skipped (idempotency guard for at-least-once delivery). On failure: `MarkPendingForRetry` while `AttemptCount + 1 < MaxAttempts`, else `MarkFailed`. Never rethrows processing failures — the outcome drives the consumer. `forceFailure` payload follows the same retry-then-DLQ path.
- `JobProcessingOptions.MaxAttempts` (env `JOB_MAX_ATTEMPTS`, default 3).
- `OutboxProcessor` / `IOutboxStore` / `IOutboxWriter` / `OutboxOptions` — outbox orchestration lives in Application; EF implementations in Infrastructure.
- `IAiAnalysisClient` + `AiAnalysisRequest`/`AiAnalysisResult` records.
- `IJobQueuePublisher` — `PublishJobSubmittedAsync`, `PublishJobRetryAsync` (republish to main queue), `PublishJobFailedAsync` (DLQ + `x-failure-reason` header).

### Infrastructure (`Jobs.Infrastructure`)

- `JobsDbContext` — `jobs` + `outbox_messages` tables. Migrations: InitialJobsSchema, AddJobLifecycleFields, AddJobResult, AddJobAttemptCount, AddOutboxMessages.
- `JobRepository`, `EfOutboxWriter` (stages in caller's unit of work), `EfOutboxStore`.
- `RabbitMqJobQueuePublisher` — declares queues on publish; DLQ name from `RabbitMqOptions.DeadLetterQueueName` (env `RABBITMQ_DLQ_NAME`, default `jobs.submitted.dlq`).
- `HttpAiAnalysisClient` — typed HttpClient, snake_case JSON matching the FastAPI contract.

### Worker (`Platform.Worker`)

- `JobSubmittedMessageConsumerService` — consumes `jobs.submitted` with `BasicQos` prefetch (env `RABBITMQ_PREFETCH`, default 4; deliberate backpressure protecting the AI service). Maps outcomes: Completed/Skipped -> ack; RetryScheduled -> republish + ack; FailedPermanently -> DLQ publish + ack. Unexpected exceptions -> nack without requeue.
- AI client registered with a resilience handler: 3 retries, exponential backoff + jitter (transient 5xx/timeout/connection failures).
- Serilog structured JSON console logging.

### AI Service (`ai-service/`, Python 3.11 / FastAPI)

- `GET /health` -> `{status, provider}`; `POST /v1/analyze` `{job_id, job_type, payload}` -> `{job_id, summary, model, latency_ms}`.
- Providers: `stub` (default — deterministic, free, no keys) and `anthropic` (`claude-haiku-4-5`, only when `AI_PROVIDER=anthropic` + `ANTHROPIC_API_KEY` set).
- 12 pytest tests; ruff-clean.

### Tests and CI

- `src/Tests/Jobs.Domain.Tests` (21), `src/Tests/Jobs.Application.Tests` (13), `src/Tests/Platform.Api.Tests` (5) — 39 xUnit tests.
- `.github/workflows/ci.yml` — dotnet build+test and python ruff+pytest jobs on push/PR to main.

### Local Development

- `docker compose up --build` runs the full stack: postgres, rabbitmq (+ management UI :15672), ai-service (:8000), api (:5200, auto-migrates), worker. Healthcheck-ordered startup.
- Infra-only + `dotnet run` flow still works (see README).
- Secrets from `.env` via DotNetEnv; `.env.example` lists every knob.

## Verified End-to-End (2026-07-06)

```text
Happy path: submit -> outbox -> dispatcher -> queue -> worker -> ai-service -> Completed with result.
Retry/DLQ: forceFailure -> 3 attempts -> Failed, attemptCount 2, 1 message in jobs.submitted.dlq.
Rate limit: 10x 201 then 429 + Retry-After for one tenant; other tenants unaffected.
Outbox: submit during RabbitMQ outage -> 201; broker restart -> dispatcher publishes -> job Completed.
```

## Not Implemented (intentionally — see completion-plan.md "Explicitly Out of Scope")

```text
Redis, auth, k6, cloud deployment, Kubernetes, frontend,
separate outbox relay deployable, OpenTelemetry/Jaeger tracing,
TTL delayed-retry queue, distributed rate limiting
```

## Outbox Tradeoff (RESOLVED)

The MVP originally published to RabbitMQ after the DB commit, accepting that a publish failure after save could lose dispatch. Milestone 5 closed this: job + outbox row commit in one transaction and the dispatcher publishes out-of-band. Delivery is now at-least-once; the consumer's non-Pending skip guard makes processing idempotent.
