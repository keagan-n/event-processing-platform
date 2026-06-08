# Next Step

Determine the smallest next implementation step based on current state and roadmap.

## What to do

### 1. Check current state

```bash
git status
git log --oneline -5
```

Report the current branch, any uncommitted changes, and recent commits.

### 2. Read roadmap

Read `.claude/context/roadmap.md` for the active implementation order.
Read `.claude/context/current-state.md` for what's already implemented.

### 3. Identify current position

Based on recent commits and current state, determine which roadmap step was last completed.

### 4. Recommend next step

State the next roadmap item and break it into the smallest concrete action:
- Which files to inspect.
- What the change would do.
- What the expected result is.
- How to verify it.

### 5. Check prerequisites

- Are there uncommitted changes that should be committed first?
- Is Docker running?
- Is the current step blocked by something else?

### 6. Present recommendation

Format:

```
Current position: {last completed roadmap item}
Next step: {roadmap item number and description}
Smallest action: {concrete first sub-task}
Files to inspect: {list}
Prerequisite: {any blockers}
```

## Important

- Do NOT start implementing. Present the recommendation and wait.
- If there are uncommitted changes, suggest handling those first.
- If the next step is ambiguous, ask a clarifying question.
- Always check that the recommended step matches the roadmap order.
