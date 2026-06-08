# Update State

After implementing a change, update all state-tracking files to stay in sync.

## Arguments

$ARGUMENTS - Description of what was just implemented.

## What to do

### 1. Understand what changed

Read the description: "$ARGUMENTS"

If no description was provided, run:

```bash
git log --oneline -3
git diff --stat
```

And infer what changed from recent commits and working tree.

### 2. Read current state files

Read all three:
- `.github/specs/current-state.md` (canonical source of truth)
- `.github/specs/queue-ai-retry-rate-limit-roadmap.md` (canonical roadmap)
- `.claude/context/current-state.md` (working memory mirror)
- `.claude/context/roadmap.md` (working memory mirror)

### 3. Determine what needs updating

For each file, check:
- Does the "Implemented" section need a new entry?
- Does the "Not Implemented" section need an item removed?
- Does the "Next Milestone" section need to advance?
- Does the roadmap need an item marked as complete?

### 4. Draft edits

For each file that needs changes, show the exact edit:
- What line(s) to change.
- What the new content would be.
- Why.

### 5. Check mirrors match

After drafting, verify `.claude/context/current-state.md` matches `.github/specs/current-state.md` and `.claude/context/roadmap.md` matches the roadmap spec.

### 6. Present changes and wait

Format:

```
Change implemented: {description}

Files to update:
  .github/specs/current-state.md:
    - {what to add/change}
  .github/specs/queue-ai-retry-rate-limit-roadmap.md:
    - {what to add/change}
  .claude/context/current-state.md:
    - {mirror update}
  .claude/context/roadmap.md:
    - {mirror update}

Files unchanged: {list any that don't need updates}
```

Do NOT apply the edits. Wait for approval.

## Important

- Do NOT edit files until approved.
- `.github/specs/` is the source of truth. Edit it first, then mirror to `.claude/context/`.
- Keep `.claude/context/` entries concise — summaries, not full copies.
- If the change doesn't affect runtime behavior, say so and skip the update.
- If the change completes a roadmap item, note which one.
