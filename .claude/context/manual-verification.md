# Manual Verification (Working Memory)

Quick checklist. See `.github/specs/manual-verification.md` for full details with commands.

## Checklist

1. `docker compose up -d` — start PostgreSQL and RabbitMQ.
2. Run API: `dotnet run --project src/Platform/API/Platform.Api.csproj`
3. Run Worker: `dotnet run --project src/Platform/Worker/Platform.Worker.csproj`
4. `GET http://localhost:5200/health` — expect `{"status":"healthy"}`.
5. `POST http://localhost:5200/jobs` with normal payload — expect `201 Created`.
6. `GET /jobs/{jobId}/status` — expect `Completed`.
7. `POST /jobs` with `forceFailure` payload — expect `201 Created`.
8. `GET /jobs/{jobId}/status` — expect `Failed`.
9. Check RabbitMQ: `Ready` = 0, `Unacked` = 0, `Consumers` >= 1.
10. Check DBeaver/psql: job rows match expected statuses.
11. `dotnet build src/Platform.sln` — expect 0 errors.
