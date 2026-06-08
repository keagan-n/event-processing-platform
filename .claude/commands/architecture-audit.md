# Architecture Audit

Grep the codebase for forbidden layer dependency violations.

## What to do

### 1. Domain must not reference infrastructure

```bash
grep -rn "using.*EntityFramework\|using.*Npgsql\|using.*Microsoft\.AspNetCore\|using.*RabbitMQ\|using.*Infrastructure\|using.*System\.Net\.Http" src/Modules/Jobs/Jobs.Domain/ --include="*.cs" || echo "PASS: Domain has no forbidden references"
```

```bash
grep -rn "DbContext\|IServiceCollection\|IHostBuilder\|HttpClient\|IChannel\|ConnectionFactory" src/Modules/Jobs/Jobs.Domain/ --include="*.cs" || echo "PASS: Domain has no infrastructure types"
```

### 2. Application must not reference infrastructure implementations

```bash
grep -rn "using.*Infrastructure\|using.*Npgsql\|using.*EntityFramework\|using.*RabbitMQ" src/Modules/Jobs/Jobs.Application/ --include="*.cs" || echo "PASS: Application has no infrastructure references"
```

```bash
grep -rn "DbContext\|JobsDbContext\|ConnectionFactory\|IChannel" src/Modules/Jobs/Jobs.Application/ --include="*.cs" || echo "PASS: Application has no infrastructure types"
```

### 3. API must not use DbContext directly

```bash
grep -rn "JobsDbContext\|DbContext" src/Platform/API/ --include="*.cs" | grep -v "obj/" | grep -v "bin/" || echo "PASS: API does not use DbContext"
```

### 4. Worker must not poll PostgreSQL

```bash
grep -rn "GetNextPending\|GetByStatus\|Task\.Delay.*poll\|while.*pending" src/Platform/Worker/ --include="*.cs" | grep -v "obj/" | grep -v "bin/" || echo "PASS: Worker has no polling patterns"
```

### 5. Worker must not mutate status directly

```bash
grep -rn "\.Status\s*=" src/Platform/Worker/ --include="*.cs" | grep -v "obj/" | grep -v "bin/" || echo "PASS: Worker does not mutate status directly"
```

### 6. Check for Redis references

```bash
grep -rn "Redis\|StackExchange\|IConnectionMultiplexer\|IDatabase" src/ --include="*.cs" | grep -v "obj/" | grep -v "bin/" || echo "PASS: No Redis references in source"
```

### 7. Check for OutboxRelay references

```bash
grep -rn "OutboxRelay\|Outbox\|outbox" src/ --include="*.cs" | grep -v "obj/" | grep -v "bin/" || echo "PASS: No outbox references in source"
```

### 8. Check for direct OpenAI SDK references

```bash
grep -rn "OpenAI\|openai\|ChatCompletion\|GPT" src/ --include="*.cs" | grep -v "obj/" | grep -v "bin/" || echo "PASS: No direct OpenAI SDK references"
```

### 9. Check leftover polling repository methods

```bash
grep -rn "GetNextPendingAsync\|GetByStatusAsync" src/Modules/Jobs/ --include="*.cs" | grep -v "obj/" | grep -v "bin/"
```

If found, note these as legacy methods that may need removal (roadmap item 1).

### 10. Report

Format:

```
Architecture Audit Results
==========================
Domain isolation:       {PASS / VIOLATIONS}
Application isolation:  {PASS / VIOLATIONS}
API boundary:           {PASS / VIOLATIONS}
Worker no-poll:         {PASS / VIOLATIONS}
Worker no-mutate:       {PASS / VIOLATIONS}
No Redis:               {PASS / VIOLATIONS}
No outbox:              {PASS / VIOLATIONS}
No direct OpenAI:       {PASS / VIOLATIONS}
Legacy polling methods: {FOUND / CLEAN}
```

For each violation, show the exact file, line number, and offending code.

## Important

- This is read-only. Do NOT fix violations — report them.
- Exclude `obj/` and `bin/` directories from all greps.
- A "FOUND" on legacy polling methods is informational, not a violation — it's roadmap item 1.
