# Architecture

## Architecture Style

This project uses a modular monolith architecture.

The goal is to keep the system simple while preserving clean boundaries between:

- API transport concerns
- Application use cases
- Domain rules
- Infrastructure implementation
- Background worker processing

## Current Modules

The repo contains these modules:

- Jobs
- Tenants
- AI
- RateLimiting

## Active MVP Module

The MVP focuses only on the `Jobs` module.

## Deferred Modules

These modules exist for future phases but are not part of the current MVP:

- Tenants
- AI
- RateLimiting

They should not be expanded unless explicitly requested.

## Layer Responsibilities

### Platform.API

Responsible for:

- HTTP endpoints
- Request/response contracts
- Mapping API requests to application commands
- Returning HTTP results

The API should not contain business logic.

### Jobs.Application

Responsible for:

- Use-case orchestration
- Commands
- Results
- Application services
- Repository abstractions

The application layer coordinates domain behavior and persistence abstractions.

### Jobs.Domain

Responsible for:

- Job entity
- Job status lifecycle
- Domain invariants
- Valid status transitions

The domain layer must not depend on ASP.NET, EF Core, PostgreSQL, Redis, or HTTP concepts.

### Jobs.Infrastructure

Responsible for:

- EF Core DbContext
- PostgreSQL mapping
- Repository implementations
- Database-specific behavior

Infrastructure implements abstractions defined by the application layer.

### Platform.Worker

Responsible for:

- Running background processing
- Polling for pending jobs
- Calling application services to process jobs

The worker should not directly mutate database rows or bypass domain logic.

## Dependency Rules

Allowed dependencies:

```text
Platform.API -> Jobs.Application
Jobs.Application -> Jobs.Domain
Jobs.Infrastructure -> Jobs.Application
Jobs.Infrastructure -> Jobs.Domain
Platform.Worker -> Jobs.Application
Platform.Worker -> Jobs.Infrastructure

Forbidden dependencies:
Jobs.Domain -> Jobs.Application
Jobs.Domain -> Jobs.Infrastructure
Jobs.Domain -> EF Core
Jobs.Domain -> ASP.NET
Jobs.Domain -> PostgreSQL
Jobs.Application -> Platform.API
Jobs.Application -> Platform.Worker

flowchart LR
    Client[Client / Postman] --> API[Platform.API]
    API --> App[Jobs.Application]
    App --> Domain[Jobs.Domain]
    App --> Infra[Jobs.Infrastructure]
    Infra --> DB[(PostgreSQL)]

flowchart LR
    Worker[Platform.Worker] --> App[Jobs.Application]
    App --> Domain[Jobs.Domain]
    App --> Infra[Jobs.Infrastructure]
    Infra --> DB[(PostgreSQL)]

sequenceDiagram
    participant Client
    participant API as Platform.API
    participant App as Jobs.Application
    participant Domain as Jobs.Domain
    participant Infra as Jobs.Infrastructure
    participant DB as PostgreSQL
    participant Worker as Platform.Worker

    Client->>API: POST /jobs
    API->>App: SubmitJobCommand
    App->>Domain: Job.Create(...)
    App->>Infra: Add Job
    Infra->>DB: Insert Pending job
    API-->>Client: 201 Created

    Worker->>App: Process next pending job
    App->>Infra: Get pending job
    Infra->>DB: Query Pending job
    App->>Domain: MarkProcessing()
    Infra->>DB: Save Processing
    App->>Domain: MarkCompleted()
    Infra->>DB: Save Completed

    Client->>API: GET /jobs/{id}/status
    API->>App: Get status
    App->>Infra: Get job by id
    Infra->>DB: Query job
    API-->>Client: Current status