# Review Diff

Review the current working tree changes before committing.

## What to do

### 1. Check status

```bash
git status
```

Report untracked files and modified files.

### 2. Show diff summary

```bash
git diff --stat
```

List files changed and lines added/removed.

### 3. Show full diff

```bash
git diff
```

Review every change for:
- Correctness: does the change do what was intended?
- Scope: are there unrelated changes mixed in?
- Safety: any secrets, credentials, or `.env` content?
- Architecture: does it respect layer boundaries (Domain has no EF/HTTP, API has no DbContext, Worker doesn't poll)?
- Deferred features: does it introduce Redis, outbox, rate limiting, or auth?

### 4. Check staged changes too

```bash
git diff --cached --stat
git diff --cached
```

### 5. Run build

```bash
dotnet build src/Platform.sln
```

Report pass or fail.

### 6. Suggest commit

If changes look good, suggest:
- A semantic commit type: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`, `test:`
- A concise commit message
- Which files to stage

Do NOT commit. Present the suggestion and wait for approval.

## Important

- Do NOT commit or push. This is review only.
- Flag any file that shouldn't be committed (`.env`, secrets, binaries).
- Flag any change that touches deferred features.
- Flag any change outside the expected scope.
