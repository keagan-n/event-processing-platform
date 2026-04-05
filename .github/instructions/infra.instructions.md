---
description: "Use when setting up databases, configuring PostgreSQL/Redis, managing migrations, designing multi-tenant schemas, or working with infrastructure code. Covers local dev setup, production patterns, and data isolation."
applyTo: "**/*.sql, **/migrations/**, **/docker-compose.yml, **/*.tf"
---
# Infrastructure & Data

## Local Development Setup (macOS)

**Prerequisites:**
```bash
# PostgreSQL 15+
brew install postgresql@15
brew services start postgresql@15

# Redis 7+
brew install redis
brew services start redis

# Verify services are running
psql postgres -c "SELECT version();"
redis-cli ping
```

**Create local development database:**
```bash
createdb event_processing_dev
createuser event_admin -P  # Prompt for password
psql event_processing_dev -c "ALTER USER event_admin CREATEDB;"
```

**Connection string for .NET:**
```
Host=localhost;Port=5432;Database=event_processing_dev;Username=event_admin;Password=<your_password>
```

## Multi-Tenant Schema Design

**Golden rule:** Every query must include `WHERE tenant_id = ?`. Use composite keys and indexes to enforce this at the database level.

### Events Table

```sql
CREATE TABLE events (
    tenant_id     UUID NOT NULL,
    event_id      UUID NOT NULL,
    event_type    VARCHAR(100) NOT NULL,
    occurred_at   TIMESTAMPTZ NOT NULL,
    received_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    payload       JSONB NOT NULL,
    status        VARCHAR(50) NOT NULL DEFAULT 'pending',  -- pending, processed, failed
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    PRIMARY KEY (tenant_id, event_id),
    CONSTRAINT events_event_type_check CHECK (event_type ~ '^[a-z._]+$')
);

-- Composite index for time-range queries by tenant
CREATE INDEX idx_events_tenant_occurred_at ON events (tenant_id, occurred_at DESC);

-- For failed event recovery
CREATE INDEX idx_events_tenant_status ON events (tenant_id, status) WHERE status IN ('pending', 'failed');
```

### Rate Quota Table

```sql
CREATE TABLE rate_quotas (
    tenant_id          UUID NOT NULL,
    quota_key          VARCHAR(100) NOT NULL,
    limit_per_minute   INT NOT NULL,
    limit_per_day      INT NOT NULL,
    current_minute_use INT NOT NULL DEFAULT 0,
    current_day_use    INT NOT NULL DEFAULT 0,
    last_reset         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    PRIMARY KEY (tenant_id, quota_key)
);

-- Reset counters by cron job (separate process)
-- Or use Redis for ephemeral minute-level tracking (prefer Redis for this)
```

### AI Results Table

```sql
CREATE TABLE ai_results (
    tenant_id       UUID NOT NULL,
    result_id       UUID NOT NULL,
    event_id        UUID NOT NULL,
    ai_model        VARCHAR(50) NOT NULL,
    decision        TEXT NOT NULL,
    confidence      NUMERIC(3,2) NOT NULL,  -- 0.00 to 1.00
    reasoning       TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    PRIMARY KEY (tenant_id, result_id),
    FOREIGN KEY (tenant_id, event_id) REFERENCES events(tenant_id, event_id)
);

CREATE INDEX idx_ai_results_tenant_created_at ON ai_results (tenant_id, created_at DESC);
```

### Audit Log Table

```sql
CREATE TABLE audit_logs (
    tenant_id       UUID NOT NULL,
    log_id          UUID NOT NULL,
    operation       VARCHAR(50) NOT NULL,     -- create, update, delete, ai_call, quota_check
    entity_type     VARCHAR(50) NOT NULL,
    entity_id       UUID,
    actor           VARCHAR(255),             -- service name or user
    details         JSONB,
    timestamp       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    PRIMARY KEY (tenant_id, log_id)
);

CREATE INDEX idx_audit_logs_tenant_timestamp ON audit_logs (tenant_id, timestamp DESC);
```

## Migrations (EF Core)

**Create migration:**
```bash
dotnet ef migrations add AddEventsTable -o Infrastructure/Migrations
```

**Apply migration:**
```bash
dotnet ef database update
```

**Rollback to previous state:**
```bash
dotnet ef database update <previous_migration_name>
```

**Safety checklist before pushing:**
- [ ] Migration has both Up() and Down() methods
- [ ] Down() tested locally: `dotnet ef database update <prev>` then `dotnet ef database update`
- [ ] No data loss on rollback
- [ ] New indexes have meaningful names
- [ ] Foreign keys exist on composite keys (tenant_id, entity_id)

## Redis for Ephemeral Data

Use Redis for quota tracking (reset per minute/second) and cache:

```csharp
// Rate limiter backed by Redis
public class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _redis;
    
    public async Task<bool> CheckQuotaAsync(string tenantId, string quotaKey, CancellationToken ct)
    {
        var key = $"quota:{tenantId}:{quotaKey}:{DateTime.UtcNow:yyyyMMdd-HH-mm}";
        var count = await _redis.StringIncrementAsync(key);
        
        if (count == 1)
            await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(2)); // Auto-expire
        
        var limit = 1000; // Example: 1k per minute
        return count <= limit;
    }
}
```

**Connection string:**
```
localhost:6379,abortConnect=false
```

**Monitoring:**
```bash
redis-cli
> INFO stats
> KEYS quota:*
> TTL quota:<tenant>:<key>:<time>
```

## Backup & Restore (Production Patterns)

**Backup production database daily:**
```bash
pg_dump -h prod-db-host -U postgres event_processing > backup-$(date +%Y%m%d).sql
gzip backup-$(date +%Y%m%d).sql
aws s3 cp backup-$(date +%Y%m%d).sql.gz s3://my-backups/
```

**Restore from backup:**
```bash
createdb event_processing_restore
psql event_processing_restore < backup-20260101.sql
```

## Docker Compose for Local Dev

```yaml
version: '3.9'

services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: event_processing_dev
      POSTGRES_USER: event_admin
      POSTGRES_PASSWORD: devpassword
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U event_admin"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

**Start local stack:**
```bash
docker-compose up -d
```

## Query Performance & Monitoring

**Identify slow queries:**
```sql
-- Enable query logging in postgresql.conf
log_min_duration_statement = 100  -- Log queries > 100ms

-- Check logs
SELECT * FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;
```

**Verify indexes are used:**
```sql
EXPLAIN ANALYZE
SELECT * FROM events
WHERE tenant_id = '12345678-1234-1234-1234-123456789012'
  AND occurred_at > NOW() - INTERVAL '1 day'
ORDER BY occurred_at DESC;
```

Should show "using index" in the plan.

**Check table bloat:**
```sql
SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename))
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

## AWS Integration (Future)

**RDS PostgreSQL:**
- Use AWS Secrets Manager for credentials
- Enable automated backups (7-day retention minimum)
- Use read replicas for reporting queries
- Multi-AZ for high availability

**ElastiCache Redis:**
- Sharding for horizontal scale
- Automatic failover
- Backup snapshots

Environment variable for connection:
```csharp
var connString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("DB_CONNECTION_STRING not set");
```

## Naming Conventions

- **Table names**: snake_case, plural (events, rate_quotas)
- **Columns**: snake_case (tenant_id, event_id, occurred_at)
- **Indexes**: idx_<table>_<columns> (idx_events_tenant_occurred_at)
- **Foreign keys**: fk_<table>_<referenced_table> (fk_ai_results_events)
- **Constraints**: ck_<table>_<rule> (ck_events_event_type_check)

## Testing Data Isolation

Always verify tenant_id filtering in integration tests:

```csharp
[Category("Integration")]
public async Task QueryEvent_DifferentTenant_ReturnsNothingAsync()
{
    var repo = new EfEventRepository(_dbContext);
    var evt = new Event { TenantId = "tenant-a", EventId = Guid.NewGuid() };
    
    await repo.SaveAsync(evt);
    
    // Switch tenant context
    var fetched = await repo.GetByIdAsync(evt.EventId, "tenant-b");
    
    Assert.IsNull(fetched, "Cross-tenant query should return nothing");
}
```
