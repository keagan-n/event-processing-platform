# Check State

Audit `.github/specs/current-state.md` against the actual codebase to find staleness.

## What to do

### 1. Read the current-state spec

Read `.github/specs/current-state.md` in full.

### 2. Verify each claim against the codebase

For each section of the spec, check the actual code:

**API endpoints**: Check `src/Platform/API/Endpoints/` and `src/Platform/API/Program.cs` for registered endpoints. Do the listed endpoints match?

**Domain entity**: Check `src/Modules/Jobs/Jobs.Domain/` for the Job entity, JobStatus enum, and domain methods. Do the listed properties and methods match?

**Application services**: Check `src/Modules/Jobs/Jobs.Application/` for interfaces and service classes. Do the listed services match?

**Infrastructure**: Check `src/Modules/Jobs/Jobs.Infrastructure/` for DbContext, repository, and RabbitMQ implementations. Do the listed implementations match?

**Worker**: Check `src/Platform/Worker/` for the consumer service. Is the description accurate (RabbitMQ consumer, not polling)?

**Docker Compose**: Check `docker-compose.yml` for services listed. Do they match the spec's claims?

### 3. Check the "Not Implemented" section

Verify that the items listed as "not implemented" are still not implemented. Grep for any signs of:
- Retry logic
- Rate limiting
- Auth middleware
- DLQ configuration
- Automated test projects

### 4. Check the "Next Milestone" section

Does the "next milestone" description match the actual roadmap in `queue-ai-retry-rate-limit-roadmap.md`?

### 5. Report findings

List each discrepancy as:
- **Section**: which part of current-state.md
- **Claim**: what the spec says
- **Reality**: what the code shows
- **Fix**: what should change in the spec

If everything matches, say "current-state.md is accurate as of {date}."

## Important

- Do NOT modify any files. This is a read-only audit.
- Be specific about file paths when reporting discrepancies.
- Check actual code, not other spec files — other specs may also be stale.
