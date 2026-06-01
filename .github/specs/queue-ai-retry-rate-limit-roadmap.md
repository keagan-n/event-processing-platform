# Queue, AI, Retry, Rate-Limit Roadmap

## Current Working State (Implemented)

```text
POST /jobs persists valid jobs in PostgreSQL.
POST /jobs publishes JobSubmittedMessage to RabbitMQ after persistence succeeds.
Platform.Worker consumes JobSubmittedMessage from RabbitMQ.
Worker loads job by ID from PostgreSQL and processes through Jobs.Application.
GET /jobs/{jobId}/status reads persisted status.
Normal jobs: Pending -> Processing -> Completed.
Forced-failure jobs: Pending -> Processing -> Failed.
PostgreSQL is the system of record for all job state.
```

## Active Implementation Order

```text
1) Queue-based job dispatch (implemented)
2) Python AI service integration
3) Retries after AI integration
4) Rate limiting after queue + AI + retries
```

Queue milestone breakdown:

```text
Queue Milestone 1: Add local RabbitMQ infrastructure through Docker Compose (implemented).
Queue Milestone 2: Queue message contract, publisher abstraction, and RabbitMQ publisher capability (implemented).
Queue Milestone 3: POST /jobs publishes JobSubmittedMessage to RabbitMQ after persistence succeeds (implemented).
Queue Milestone 4: Worker consumes RabbitMQ messages instead of PostgreSQL polling (implemented).
```

## Current Architecture

```text
API persists job in PostgreSQL
API publishes queue message containing jobId
Worker consumes queue messages from RabbitMQ
Worker loads job by jobId from PostgreSQL
Worker processes job through Jobs.Application
Python AI service eventually replaces simulated delay
```

Preferred local queue direction:

```text
RabbitMQ through Docker Compose first.
AWS SQS can be discussed later as a cloud-backed alternative.
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
