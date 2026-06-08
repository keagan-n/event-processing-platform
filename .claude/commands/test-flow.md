# Test Flow

Run the full end-to-end job lifecycle verification against the running local environment.

## What to do

### 1. Infrastructure checks

Check that Docker containers are running:

```bash
docker ps --format '{{.Names}}' | grep -E 'postgres|rabbitmq'
```

Expected: `event-platform-postgres` and `event-platform-rabbitmq` both present.
If either is missing, tell the user to run `docker compose up -d` and stop.

### 2. API health check

```bash
curl -s http://localhost:5200/health
```

Expected: `{"status":"healthy"}`.
If unreachable, tell the user to start the API with `dotnet run --project src/Platform/API/Platform.Api.csproj` and stop.

### 3. Submit a normal test job

```bash
curl -s -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-test","jobType":"ai.summary","payload":"{\"text\":\"Test flow verification\"}"}'
```

Capture the `jobId` from the 201 response.

### 4. Poll normal job status

Poll `GET http://localhost:5200/jobs/{jobId}/status` every 2 seconds, up to 30 seconds.
Expected terminal state: `Completed`.

### 5. Submit a forced-failure job

```bash
curl -s -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-test","jobType":"ai.summary","payload":"{\"text\":\"Failure test\",\"forceFailure\":true}"}'
```

Capture the `jobId`.

### 6. Poll failure job status

Poll `GET http://localhost:5200/jobs/{jobId}/status` every 2 seconds, up to 30 seconds.
Expected terminal state: `Failed`.

### 7. PostgreSQL verification

Tell the user how to verify in PostgreSQL directly. Provide the exact commands:

```bash
docker exec -it event-platform-postgres psql -U postgres -d event_processing -c "SELECT \"JobId\", \"Status\", \"CreatedUtc\", \"StartedUtc\", \"CompletedUtc\", \"FailedUtc\", \"FailureReason\" FROM \"Jobs\" ORDER BY \"CreatedUtc\" DESC LIMIT 5;"
```

Explain what each column means:
- **JobId**: The unique job identifier.
- **Status**: Should show the terminal state (Completed or Failed).
- **CreatedUtc**: When POST /jobs persisted the job.
- **StartedUtc**: When the Worker picked it up (MarkProcessing).
- **CompletedUtc**: When processing finished successfully (null for failed jobs).
- **FailedUtc**: When processing failed (null for completed jobs).
- **FailureReason**: The failure message (null for completed jobs).

Run the query and show the user the results.

### 8. RabbitMQ verification

Tell the user how to verify RabbitMQ state. Provide the exact commands:

```bash
curl -s -u guest:guest http://localhost:15672/api/queues/%2f/jobs-submitted | python3 -m json.tool
```

Explain the key fields:
- **messages**: Number of messages currently in the queue. Should be 0 after processing.
- **messages_ready**: Messages waiting to be consumed. Should be 0.
- **messages_unacknowledged**: Messages being processed. Should be 0 after completion.
- **message_stats.deliver_get**: Total messages delivered to consumers (should increase with each test).
- **message_stats.ack**: Total acknowledged messages (should match deliver_get).
- **message_stats.publish**: Total messages published (should match number of POST /jobs calls).

If the queue doesn't exist yet, explain that it gets created on first publish/consume and to check that the Worker is running.

Also mention the RabbitMQ management UI:
```
http://localhost:15672
username: guest
password: guest
```

The Queues tab shows the `jobs-submitted` queue with message rates and counts.

### 9. Report results

Summarize:
- Normal job: expected Completed, actual status
- Failure job: expected Failed, actual status
- PostgreSQL rows: match or mismatch
- RabbitMQ queue: drained or messages stuck
- Any unexpected behavior

If everything passed, say "Full flow verified." with the timestamp.
If anything failed, say exactly what went wrong and suggest next steps.

## Important

- Do NOT start Docker, the API, or the Worker yourself. Only check if they're running and tell the user how to start them.
- Do NOT modify any code. This is a read-only verification skill.
- Do NOT skip the PostgreSQL and RabbitMQ verification steps — they catch bugs the HTTP responses can't.
- The Worker must be running separately for jobs to transition past Pending. If jobs stay Pending, tell the user to start the Worker: `dotnet run --project src/Platform/Worker/Platform.Worker.csproj`
