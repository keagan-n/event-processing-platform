# Scope Check

Check a proposed change against deferred features, roadmap order, and architecture rules.

## Arguments

$ARGUMENTS - Description of the proposed change.

## What to do

### 1. State the proposal

"Proposed change: $ARGUMENTS"

### 2. Read roadmap and constraints

Read:
- `.claude/context/roadmap.md`
- `.claude/context/architecture-rules.md`
- `.claude/context/current-state.md`

### 3. Check deferred features

Does the proposal touch any of these? Flag each match:

- [ ] Redis or Redis-backed anything
- [ ] OutboxRelay or outbox pattern
- [ ] Dead-letter queue
- [ ] Authentication or authorization
- [ ] k6 or performance tests
- [ ] Cloud deployment
- [ ] Distributed tracing
- [ ] Advanced multi-tenancy
- [ ] Rate limiting (unless it's the active roadmap step)

### 4. Check roadmap order

What is the current next roadmap step? Does the proposal match it, or does it skip ahead?

If skipping: flag it and explain which steps are being bypassed.

### 5. Check architecture boundaries

Does the proposal violate any hard rules?

- Domain referencing infrastructure?
- API using DbContext directly?
- Worker polling PostgreSQL?
- Direct OpenAI SDK calls from .NET?
- Business logic in API endpoints?

### 6. Check scope size

Is this a small, focused change? Or does it touch multiple unrelated concerns?

If broad: suggest how to break it into smaller steps.

### 7. Report

Format:

```
Proposal: {description}

Deferred features: {CLEAN / flagged items}
Roadmap order:     {IN ORDER / SKIPPING: items skipped}
Architecture:      {CLEAN / VIOLATIONS: details}
Scope size:        {SMALL / BROAD: suggestion to split}

Verdict: {PROCEED / REVIEW NEEDED / BLOCKED}
Reason: {one sentence}
```

### 8. If blocked or needs review

Explain exactly what's wrong and suggest the correct approach:
- Which roadmap step to do first.
- How to avoid the deferred feature.
- How to stay within architecture boundaries.
- How to scope it smaller.

Be aggressive. Do not hand-wave. If it's a weak decision, say so and say why.

## Important

- This is advisory. Do NOT edit any files.
- Be direct about problems. The user wants pushback on weak decisions.
- "It would be nice to add" is not a reason to skip roadmap order.
- If the proposal is fine, say so clearly and move on.
