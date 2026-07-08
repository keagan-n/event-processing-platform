# Manual Verification Checklist

End-to-end local verification for the current MVP.

## Prerequisites

```bash
docker compose up -d
docker ps
# Expected: event-platform-postgres, event-platform-rabbitmq
```

## Start Services

Terminal 1 — API:
```bash
dotnet run --project src/Platform/API/Platform.Api.csproj
```

Terminal 2 — Worker:
```bash
dotnet run --project src/Platform/Worker/Platform.Worker.csproj
```

## Health Check

```bash
curl -s http://localhost:5200/health
# Expected: {"status":"healthy"}
```

## Submit Normal Job

```bash
curl -s -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-test","jobType":"ai.summary","payload":"{\"text\":\"Test verification\"}"}'
# Expected: 201 Created, status "Pending"
# Capture jobId from response.
```

## Verify Normal Job Completed

```bash
curl -s http://localhost:5200/jobs/{jobId}/status
# Expected: status "Completed"
# Wait a few seconds if still "Processing".
```

## Submit Forced-Failure Job

```bash
curl -s -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-test","jobType":"ai.summary","payload":"{\"text\":\"Failure test\",\"forceFailure\":true}"}'
# Expected: 201 Created, status "Pending"
# Capture jobId from response.
```

## Verify Failed Job

```bash
curl -s http://localhost:5200/jobs/{jobId}/status
# Expected: status "Failed", failureReason present.
```

## RabbitMQ Queue Check

```bash
curl -s -u guest:guest http://localhost:15672/api/queues/%2f/jobs-submitted | python3 -m json.tool
```

Check:
- `messages` — should be 0 after processing.
- `messages_ready` — should be 0.
- `messages_unacknowledged` — should be 0.
- `consumers` — should be >= 1 when Worker is running.

Or use the management UI at `http://localhost:15672` (guest/guest).

## PostgreSQL Check

```bash
docker exec -it event-platform-postgres psql -U postgres -d event_processing \
  -c "SELECT \"JobId\", \"Status\", \"CreatedUtc\", \"StartedUtc\", \"CompletedUtc\", \"FailedUtc\", \"FailureReason\" FROM \"Jobs\" ORDER BY \"CreatedUtc\" DESC LIMIT 5;"
```

Or use DBeaver to inspect the `Jobs` table.

## Build Check

```bash
dotnet build src/Platform.sln
# Expected: Build succeeded, 0 errors.
```

## Final-Scope Verification (verified 2026-07-06)

Prereq: `docker compose up -d postgres rabbitmq ai-service`, migrations applied, API + Worker running (or full `docker compose up --build`).

1. Happy path with AI result:
   - POST /jobs with a normal payload -> 201 Pending.
   - Within ~5s, GET status -> `Completed` with a non-null `result` (stub summary) and `attemptCount: 0`.

2. Retry then dead-letter:
   - POST /jobs with payload `{"forceFailure": true}` -> 201.
   - Within ~10s, GET status -> `Failed`, `attemptCount: 2` (3 attempts total).
   - RabbitMQ management UI (localhost:15672, guest/guest) -> Queues -> `jobs.submitted.dlq` has 1 message with an `x-failure-reason` header.

3. Per-tenant rate limit:
   - 12 rapid POSTs for one tenant -> first 10 return 201, then 429 with `Retry-After: 60` and JSON body naming the tenant.
   - A POST for a different tenant during the same window -> 201.

4. Outbox survives a broker outage:
   - `docker stop event-platform-rabbitmq`, POST /jobs -> 201 (job + outbox row committed).
   - `docker start event-platform-rabbitmq` -> within ~25s the dispatcher publishes and GET status -> `Completed`.

5. AI service outage retry (optional):
   - `docker stop event-platform-ai-service`, POST /jobs -> retries with backoff, then `Failed` + DLQ if the outage persists; restart the service and submit again -> `Completed`.
