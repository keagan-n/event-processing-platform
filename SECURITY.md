# Security and Environment Handling

This is a local portfolio demo with synthetic data. The API and AI service have no authentication or authorization. Tenant IDs are caller-supplied labels and can be changed to bypass a tenant's budget; job IDs do not provide tenant access control. Keep the published ports on `127.0.0.1`. Public deployment requires an identity/access-control design, TLS, rotated service credentials, and resource/abuse controls.

## Audit Results — 2026-10-10

| Check | Result |
| --- | --- |
| Gitleaks 8.30.1, all local Git refs (31 commits) and working files including ignored `.env` | No real secrets detected |
| NuGet audit, all 13 solution projects including transitive dependencies | No known vulnerable packages |
| pip-audit 2.10.1, resolved runtime and development dependencies | No known vulnerable packages |
| Bandit, Python application code | No findings |
| Trivy 0.75.0, five final Linux arm64 runtime images, all severities and unfixed findings | Zero known vulnerabilities; no CVE suppressions |
| Application checks | 61 xUnit + 18 pytest passed; full Compose smoke check including broker outage passed |

Scans reflect the advisory databases and image builds available on this date. They cannot prove the absence of unknown vulnerabilities. The local image results cover Linux arm64; CI builds/scans its own runner architecture. Build stages, the host OS/Docker installation, external LLM infrastructure, and secrets in remote-only history are outside these image/local-history checks.

Dependency and image fixes include patched Microsoft.OpenApi, FastAPI/Starlette, python-dotenv, and pytest; a supported RabbitMQ 4 image; patched Alpine runtimes; removal of PostgreSQL's vulnerable bundled Go helper in favor of `su-exec`; and removal of pip, setuptools, wheel, and ensurepip from the deployed Python image. Development tooling is installed separately through `ai-service/requirements-dev.txt`.

## Secrets and Configuration

- `.env.example` contains public local-demo placeholders, including PostgreSQL and RabbitMQ credentials. They are not suitable for shared or public deployments.
- `.env`, environment variants, private key/certificate files, and credential JSON files are ignored by Git and excluded from Docker build contexts. Ignore rules do not remove files that are already tracked; Gitleaks provides a separate check.
- Keep a real key only in your local ignored `.env` or process environment. Restrict the file with `chmod 600 .env`. Never put keys in source, screenshots, issue text, logs, or `.env.example`.
- .NET uses `Env.NoClobber().TraversePath().Load()`: existing process variables override file values. Compose resolves shell variables before its env file. To force a free demo even if your shell selects a paid provider, run:

  ```bash
  AI_PROVIDER=stub ANTHROPIC_API_KEY= docker compose --env-file .env.example up --build -d --wait
  ```

- Only the AI container receives `ANTHROPIC_API_KEY`; .NET communicates with the Python service. The optional Anthropic provider sends job payloads to that external service. Use synthetic data for the demo.
- Local environment variables are visible to processes/container administrators with sufficient access. Compose env configuration is not a production secret manager.

## Runtime Controls and Data Preservation

All five services publish host ports only on loopback and use `no-new-privileges`. API, worker, and AI run as non-root, drop capabilities, use read-only root filesystems, and have a bounded `/tmp`. API requests are capped at 256 KiB; decoded job payloads at 64 KiB UTF-8. Python request fields also have length bounds. There is no global ingress limiter, storage quota, or total AI body limit before parsing; these services assume trusted local callers.

Digest-pinned Alpine runtime bases receive distro package upgrades during builds. Update pinned bases regularly and rebuild with `--no-cache` to refresh package updates. CI audits dependencies and scans all five final images, including low-severity and unfixed findings; it does not suppress CVEs. GitHub Actions and scanner images are pinned, workflow permissions are read-only, and checkout credentials are not persisted.

The PostgreSQL Alpine and RabbitMQ 4 images use fresh volumes named `event_platform_postgres_alpine_data` and `rabbitmq_v4_data`. The previous `event_platform_postgres_data` and `rabbitmq_data` declarations remain; old data has not been migrated or deleted. Back up/export using the matching old images before planning a migration. PostgreSQL locale/collation changes need review, and a RabbitMQ 3 data volume must not be attached directly to RabbitMQ 4. `docker compose down` preserves volumes; avoid `down -v` when preserving data.

## Reproduce the Checks

From the repository root:

```bash
dotnet restore src/Platform.sln -p:NuGetAudit=true -p:NuGetAuditMode=all -warnaserror
dotnet list src/Platform.sln package --vulnerable --include-transitive
dotnet build src/Platform.sln
dotnet test src/Platform.sln

python3.11 -m venv ai-service/.venv
ai-service/.venv/bin/python -m pip install -r ai-service/requirements-dev.txt
ai-service/.venv/bin/pip-audit -r ai-service/requirements-dev.txt
ai-service/.venv/bin/ruff check ai-service scripts
(cd ai-service && .venv/bin/python -m pytest)

gitleaks git . --log-opts=--all --config .gitleaks.toml --redact=100
gitleaks dir . --config .gitleaks.toml --redact=100

AI_PROVIDER=stub ANTHROPIC_API_KEY= docker compose --env-file .env.example up --build -d --wait
python3 scripts/smoke_test.py --broker-outage --env-file .env.example
for image in $(docker compose --env-file .env.example images -q | sort -u); do
  trivy image --scanners vuln --exit-code 1 "$image"
done
```

Install Gitleaks/Trivy or use the digest-pinned Docker invocations in `.github/workflows/security.yml` and `.github/workflows/ci.yml`. CI scans saved image archives without mounting the Docker daemon socket. `.gitleaks.toml` allows only the exact `guest:guest` curl demo credential in specific documentation files; default detection rules remain enabled.

If a real secret is exposed, revoke/rotate it at its provider first, then remove it from tracked files/history as needed and rerun both scans. Avoid publishing sensitive values in reports.
