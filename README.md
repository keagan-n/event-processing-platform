# Event Processing Platform

[![CI](https://github.com/keagan-n/event-processing-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/keagan-n/event-processing-platform/actions/workflows/ci.yml)

An event-driven job-processing platform built with **C#/.NET 10**, **PostgreSQL**, **RabbitMQ**, and a **Python/FastAPI AI service**, structured as a modular monolith. Jobs are submitted over HTTP, persisted transactionally with an outbox, dispatched through RabbitMQ, processed by a worker that calls an AI analysis service, and protected end-to-end by retries, a dead-letter queue, per-tenant rate limiting, and backpressure.

The entire stack runs locally with one command and zero paid services.

```bash
docker compose up --build
```

## Architecture

```mermaid
flowchart LR
    Client([Client]) -->|POST /jobs| API

    subgraph API["Platform.Api"]
        RL[Per-tenant rate limiter]
        OD[Outbox dispatcher]
    end

    API -->|"1: job + outbox row (one transaction)"| PG[(PostgreSQL)]
    OD -->|"2: publish"| Q[/"jobs.submitted"/]

    subgraph MQ["RabbitMQ"]
        Q
        DLQ[/"jobs.submitted.dlq"/]
    end

    Q -->|"3: consume (prefetch-bounded)"| W["Platform.Worker"]
    W -->|"4: POST /v1/analyze (retry w/ backoff)"| AI["ai-service (Python/FastAPI)"]
    W -->|"5: status + result"| PG
    W -->|"max attempts exceeded"| DLQ
    Client -->|GET /jobs/id/status| API
```

**Flow:** `POST /jobs` validates and persists the job **plus** an outbox row in a single PostgreSQL transaction. A background dispatcher publishes staged messages to RabbitMQ. The worker consumes with a bounded prefetch, loads the job by ID, and calls the Python AI service for analysis. Success stores the result; failure retries up to 3 attempts and then dead-letters. `GET /jobs/{id}/status` reads the outcome.

## Distributed-Systems Concepts Demonstrated

| Concept | Where |
| --- | --- |
| Transactional outbox (atomic DB save + publish) | [JobSubmissionService](src/Modules/Jobs/Jobs.Application/JobSubmissionService.cs), [OutboxDispatcherService](src/Platform/API/OutboxDispatcherService.cs) |
| At-least-once delivery + idempotent consumer | [JobProcessingService](src/Modules/Jobs/Jobs.Application/JobProcessingService.cs) (non-Pending skip guard) |
| Retry with exponential backoff + jitter (transient HTTP) | [Worker Program.cs](src/Platform/Worker/Program.cs) (resilience handler on the AI client) |
| Message-level retry + dead-letter queue | [JobProcessingService](src/Modules/Jobs/Jobs.Application/JobProcessingService.cs), [JobSubmittedMessageConsumerService](src/Platform/Worker/JobSubmittedMessageConsumerService.cs) |
| Per-tenant rate limiting (token bucket, 429 + Retry-After) | [TenantRateLimiter](src/Platform/API/RateLimiting/TenantRateLimiter.cs) |
| Backpressure (bounded prefetch protecting downstream) | [JobSubmittedMessageConsumerService](src/Platform/Worker/JobSubmittedMessageConsumerService.cs) |
| Cross-language service boundary (LLM calls isolated in Python) | [ai-service](ai-service/), [HttpAiAnalysisClient](src/Modules/Jobs/Jobs.Infrastructure/AI/HttpAiAnalysisClient.cs) |
| Structured JSON logging | Serilog in [API](src/Platform/API/Program.cs) and [Worker](src/Platform/Worker/Program.cs) |

## Quickstart Demo

Prereqs: Docker. Copy the env file and start everything:

```bash
cp .env.example .env
docker compose up --build
```

This starts PostgreSQL, RabbitMQ (management UI at [localhost:15672](http://localhost:15672), guest/guest), the Python AI service, the API (migrations auto-applied), and the worker.

**1. Submit a job and watch it complete with an AI result:**

```bash
curl -s -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-demo","jobType":"analysis","payload":"{\"text\":\"quarterly loan portfolio risk review\"}"}'

# Poll with the returned jobId — status becomes Completed with a result
curl -s http://localhost:5200/jobs/<jobId>/status
```

**2. Watch a failing job retry and dead-letter:**

```bash
curl -s -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-demo","jobType":"analysis","payload":"{\"forceFailure\":true}"}'

# After ~5s: status shows Failed with attemptCount 2 (3 attempts total),
# and jobs.submitted.dlq holds one message (see the management UI → Queues).
```

**3. Trip the per-tenant rate limit (10 requests/minute per tenant):**

```bash
for i in $(seq 1 12); do
  curl -s -o /dev/null -w "%{http_code} " -X POST http://localhost:5200/jobs \
    -H "Content-Type: application/json" \
    -d '{"tenantId":"tenant-burst","jobType":"analysis","payload":"{\"n\":1}"}'
done
# → 201 ×10, then 429 with a Retry-After header. Other tenants are unaffected.
```

**4. Prove the outbox (submit during a broker outage):**

```bash
docker stop event-platform-rabbitmq
curl -s -X POST http://localhost:5200/jobs -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-outbox","jobType":"analysis","payload":"{\"text\":\"submitted during outage\"}"}'
# → 201. The job and its outbox row committed atomically.
docker start event-platform-rabbitmq
# ~20s later the dispatcher publishes the staged message and the job completes.
```

## Design Decisions

- **Modular monolith over microservices.** Module boundaries (Domain / Application / Infrastructure per module) give the architectural discipline of services without the operational overhead; the one real service boundary (Python AI service) exists because it isolates a different language runtime and failure domain.
- **Transactional outbox over publish-after-save.** The MVP originally published to RabbitMQ after the DB commit, which can silently lose dispatch if the broker is down. The outbox stages the message in the same transaction; an in-process dispatcher publishes it out-of-band. Delivery becomes at-least-once, which the consumer's idempotency guard absorbs.
- **Attempts tracked in PostgreSQL, not message headers.** The database is the system of record; a retry is a plain republish and the job row carries `attempt_count`. Simpler than header bookkeeping and survives broker restarts.
- **In-memory rate limiting over Redis.** A `PartitionedRateLimiter` token bucket per tenant is correct for a single API instance and is an honest answer at this scale; distributed rate limiting is listed as future work rather than prematurely built.
- **Thin messages (jobId only).** Consumers load state from the source of truth, so stale payloads in flight are impossible.
- **AI provider abstraction.** The Python service defaults to a deterministic stub (free, testable, CI-friendly); setting `AI_PROVIDER=anthropic` + an API key swaps in Claude without touching .NET.

## Known Limitations / Future Work

- Single API instance assumed for rate limiting (distributed limiter would need shared state).
- Retries redeliver immediately (in-process backoff already spaces real attempts); a TTL-based delayed-retry queue is a known upgrade.
- No distributed tracing (OpenTelemetry across the broker is the natural next step).
- Multiple competing worker instances work at-least-once but haven't been load-verified.
- No auth by design — this project demonstrates processing patterns, not identity.

## Local Development (without containerized API/worker)

```bash
docker compose up -d postgres rabbitmq ai-service   # infrastructure only
dotnet build src/Platform.sln
dotnet ef database update \
  --project src/Modules/Jobs/Jobs.Infrastructure/Jobs.Infrastructure.csproj \
  --startup-project src/Platform/API/Platform.Api.csproj --context JobsDbContext
dotnet run --project src/Platform/API/Platform.Api.csproj      # terminal 1
dotnet run --project src/Platform/Worker/Platform.Worker.csproj # terminal 2
```

## Tests

```bash
dotnet test src/Platform.sln          # 39 tests: domain, application, API
cd ai-service && python3 -m pytest    # 12 tests: FastAPI service
```

CI runs both suites plus `ruff` linting on every push and pull request.

## Repository Layout

```text
src/Modules/Jobs/        Jobs module: Domain, Application, Contracts, Infrastructure
src/Platform/API/        HTTP transport, rate limiting, outbox dispatcher
src/Platform/Worker/     RabbitMQ consumer host
src/Tests/               xUnit test projects
ai-service/              Python FastAPI analysis service (stub + Anthropic providers)
.github/specs/           Living specs: current state, completion plan, verification
```
