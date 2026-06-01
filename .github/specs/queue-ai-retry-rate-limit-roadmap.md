# Queue, AI, Retry, Rate-Limit Roadmap

## Current Working MVP (Implemented)

```text
POST /jobs persists valid jobs in PostgreSQL.
GET /jobs/{jobId}/status reads persisted status.
Platform.Worker processes jobs using PostgreSQL polling.
Normal jobs: Pending -> Processing -> Completed.
Forced-failure jobs: Pending -> Processing -> Failed.
```

PostgreSQL polling worker behavior is the current MVP, not the final queue-based direction.

## Active Implementation Order

```text
1) Queue-based job dispatch
2) Python AI service integration
3) Retries after AI integration
4) Rate limiting after queue + AI + retries
```

Queue milestone breakdown:

```text
Queue Milestone 1: Add local RabbitMQ infrastructure through Docker Compose.
Queue Milestone 2 (next): Introduce queue contracts and publisher abstraction.
```

## Target Next Architecture (Not Yet Implemented)

```text
API persists job in PostgreSQL
API publishes queue message containing jobId
Worker consumes queue messages
Worker loads job by jobId
Worker processes job through Jobs.Application
Python AI service eventually replaces simulated delay
```

Preferred local queue direction:

```text
RabbitMQ through Docker Compose first.
AWS SQS can be discussed later as a cloud-backed alternative.
```

Current queue implementation status:

```text
RabbitMQ local infrastructure exists.
API queue publishing is not implemented yet.
Worker queue consumption is not implemented yet.
Worker still uses PostgreSQL polling in the current MVP.
```

## Scope Decisions

Removed from active scope:

```text
Redis
OutboxRelay
Outbox pattern
```

Still in roadmap, later:

```text
Rate limiting
```

Rate-limiting constraint:

```text
Do not use Redis-backed rate limiting unless explicitly redesigned.
Prefer PostgreSQL-backed or simple in-memory MVP rate limiting.
```

## Outbox Tradeoff (Intentional for Portfolio MVP)

```text
Without outbox, DB save + queue publish is not atomic.
A job could be saved but queue publish could fail.
This simpler tradeoff is accepted for this portfolio MVP.
In production, outbox/transactional messaging would solve this.
Outbox is intentionally out of active scope.
```
