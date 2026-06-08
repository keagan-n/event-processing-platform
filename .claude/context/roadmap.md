# Active Roadmap (Working Memory)

Mirror of `.github/specs/queue-ai-retry-rate-limit-roadmap.md`. If conflict, the spec wins.

## Active Implementation Order

1. Remove any leftover polling repository API if still present.
2. Add Python AI service endpoint.
3. Add AI service to Docker Compose.
4. Add C# AI processing client abstraction.
5. Replace simulated job delay with Python AI service call.
6. Persist AI job result.
7. Add retry handling.
8. Add non-Redis rate limiting.
9. Add observability and verification docs.
10. Final README/resume/interview polish.

## Completed

- Queue-based dispatch (Milestones 1-4 complete).
- RabbitMQ local infrastructure.
- Queue message contract and publisher.
- API publishes after persistence.
- Worker consumes RabbitMQ instead of polling.

## Constraints

- Rate limiting must not be Redis-backed unless explicitly redesigned.
- AI calls go through Python `ai-service`, not direct OpenAI SDK from .NET.
- Outbox is intentionally out of scope.
