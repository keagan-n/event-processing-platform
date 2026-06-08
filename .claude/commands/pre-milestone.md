# Pre-Milestone Checklist

Run the "before coding" checklist before starting any implementation task. This enforces the workflow from CLAUDE.md.

## Arguments

$ARGUMENTS - Description of the task or milestone you're about to start.

## What to do

### 1. Identify the task

State the task clearly: "$ARGUMENTS"

### 2. Read current state

Read these files:
- `.github/specs/current-state.md`
- `.github/specs/queue-ai-retry-rate-limit-roadmap.md`

### 3. Check roadmap order

Verify the task matches the next item in the active roadmap. If it doesn't, warn the user that they may be skipping a step and ask for confirmation.

### 4. Inspect relevant files

Based on the task description, identify and read the files that will be affected. List them.

### 5. State current behavior

For each file that will change, describe what it currently does.

### 6. List files to change

List exact file paths that need to be created or modified, with a one-line description of each change.

### 7. Explain why

For each change, explain why it's needed for this task.

### 8. Check for scope creep

Review the proposed changes against the deferred features list:
- Redis
- Outbox / OutboxRelay
- Dead-letter queue
- Auth
- k6 tests
- Cloud deployment
- Rate limiting (unless it's the active milestone)

If any proposed change touches a deferred feature, flag it.

### 9. Present the plan

Format the plan as:

```
Task: {description}
Roadmap position: {where this falls}
Files to change:
  - {path}: {what changes}
  - {path}: {what changes}
Scope check: {clean / flagged items}
Ready to proceed: {yes / waiting for user input}
```

### 10. Wait for approval

Do NOT start coding. Wait for the user to approve or modify the plan.

## Important

- This skill produces a plan, not code.
- Do NOT edit any files.
- Do NOT skip the roadmap order check.
- If the task is ambiguous, ask clarifying questions instead of guessing.
