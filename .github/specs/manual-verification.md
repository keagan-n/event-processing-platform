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
