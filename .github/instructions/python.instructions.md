---
description: "Use when writing Python code, orchestrating AI workflows, or building Python services. Covers async patterns, feature engineering, API client design, and integration with .NET backend."
applyTo: "**/*.py"
---
# Python Development

## Project Structure

```
python/
├── src/
│   ├── ai/                    # Feature extraction, AI client wrappers
│   ├── workflows/             # Event orchestration, multi-step pipelines
│   ├── utils/                 # Shared utilities, validation
│   └── clients/               # Integrations with .NET backend, external APIs
├── tests/
│   ├── unit/
│   └── integration/
├── requirements.txt           # Pin versions for reproducibility
├── Makefile                   # Common tasks: test, lint, format
└── pyproject.toml             # Package metadata, tool config
```

## Virtual Environment (macOS)

```bash
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt

# Deactivate when done
deactivate
```

## Async Patterns

Use `asyncio` for I/O; especially important for rate-limited AI calls:

```python
# ✓ Correct: async context, proper error handling
import asyncio
from typing import Any

async def process_event_with_ai(event: dict, tenant_id: str) -> dict:
    """
    Extract features, call AI, validate result.
    Raises: QuotaExceededError, AiServiceError
    """
    # 1. Feature extraction (CPU-bound, but batched)
    features = extract_features(event)
    
    # 2. Check quota
    quota_ok = await check_quota_async(tenant_id, "ai_invocation")
    if not quota_ok:
        raise QuotaExceededError(f"Tenant {tenant_id} exceeded AI quota")
    
    # 3. Call AI (I/O-bound, must be async)
    try:
        ai_result = await ai_client.complete(
            prompt=build_prompt(features),
            tenant_id=tenant_id,
            timeout=10
        )
    except asyncio.TimeoutError:
        logger.warning(f"AI timeout for tenant {tenant_id}")
        raise AiServiceError("AI inference timeout")
    
    # 4. Validate and return
    if ai_result.confidence < 0.7:
        logger.info(f"Low confidence result for tenant {tenant_id}")
    return {
        "event_id": event["id"],
        "tenant_id": tenant_id,
        "ai_decision": ai_result.decision,
        "confidence": ai_result.confidence
    }

# ✗ Avoid: sync I/O, missing error context
def process_event_with_ai(event):
    features = extract_features(event)
    ai_result = ai_client.complete(features)  # Blocks
    return ai_result
```

## Feature Extraction

Structured inputs, not raw text. Validate against schema:

```python
from dataclasses import dataclass
from typing import Optional

@dataclass
class EventFeatures:
    """Extracted features for AI model input."""
    event_type: str
    timestamp: str  # ISO 8601
    event_value: float
    categorical_attr: str
    optional_metadata: Optional[dict] = None
    
    @classmethod
    def from_event(cls, event: dict) -> "EventFeatures":
        """Extract and validate features from raw event."""
        try:
            return cls(
                event_type=event["type"],
                timestamp=event["occurred_at"],
                event_value=float(event["value"]),
                categorical_attr=event["category"]
            )
        except (KeyError, ValueError) as e:
            raise FeatureExtractionError(f"Invalid event: {e}") from e

# Validate with Pydantic (recommended)
from pydantic import BaseModel, Field

class EventPayload(BaseModel):
    type: str = Field(..., min_length=1)
    occurred_at: str  # Validated as ISO 8601 by your JSON schema
    value: float
    category: str
```

## AI Client Design

Wrap AI SDK calls; handle quotas, retries, fallbacks:

```python
import asyncio
import logging
from typing import Any, Optional

logger = logging.getLogger(__name__)

class AiClient:
    """Wrapper around OpenAI/Claude API with tenant isolation."""
    
    def __init__(self, api_key: str, timeout: int = 10):
        self.api_key = api_key
        self.timeout = timeout
        self._client = None  # Lazy-init
    
    async def complete(
        self,
        prompt: str,
        tenant_id: str,
        max_tokens: int = 256,
        temperature: float = 0.7
    ) -> dict:
        """
        Call AI model with tenant context and timeout.
        
        Returns:
            {"decision": str, "confidence": float, "reasoning": str}
        
        Raises:
            QuotaExceededError: Tenant has no remaining quota
            AiServiceError: API timeout or error
        """
        # Check quota before calling (avoid wasted API credits)
        if not await check_quota_async(tenant_id, "ai_call"):
            raise QuotaExceededError(tenant_id)
        
        try:
            # Call with timeout
            response = await asyncio.wait_for(
                self._call_openai(prompt, max_tokens, temperature),
                timeout=self.timeout
            )
            
            # Parse and validate response
            result = self._parse_response(response)
            logger.info(
                f"AI inference success",
                extra={"tenant_id": tenant_id, "confidence": result["confidence"]}
            )
            return result
            
        except asyncio.TimeoutError:
            logger.error(f"AI timeout for tenant {tenant_id}")
            raise AiServiceError("Inference timeout") from None
        except Exception as e:
            logger.error(f"AI error for tenant {tenant_id}: {e}")
            raise AiServiceError(f"Inference failed: {e}") from e
    
    async def _call_openai(self, prompt: str, max_tokens: int, temperature: float):
        # Implementation: use openai async client
        pass
    
    def _parse_response(self, response: Any) -> dict:
        """Validate AI response structure."""
        return {
            "decision": response.choices[0].message.content,
            "confidence": 0.85,  # Extract from response if available
            "reasoning": response.usage.prompt_tokens
        }
```

## Integration with .NET Backend

Use `httpx` for async HTTP calls to your .NET API:

```python
import httpx
from typing import Optional

class BackendClient:
    """Async HTTP client for .NET backend integration."""
    
    def __init__(self, base_url: str, api_key: str):
        self.base_url = base_url
        self.api_key = api_key
    
    async def fetch_event(self, tenant_id: str, event_id: str) -> dict:
        """Fetch event from backend."""
        async with httpx.AsyncClient() as client:
            response = await client.get(
                f"{self.base_url}/api/events/{event_id}",
                headers={"Authorization": f"Bearer {self.api_key}"},
                params={"tenant_id": tenant_id},
                timeout=10.0
            )
            response.raise_for_status()
            return response.json()
    
    async def save_ai_result(self, tenant_id: str, payload: dict) -> dict:
        """POST AI result back to backend."""
        async with httpx.AsyncClient() as client:
            response = await client.post(
                f"{self.base_url}/api/ai-results",
                json=payload,
                headers={"Authorization": f"Bearer {self.api_key}"},
                params={"tenant_id": tenant_id}
            )
            response.raise_for_status()
            return response.json()
```

## Logging and Context

Use structured logging with tenant+operation ID:

```python
import logging
import logging.config
import json

logging.config.dictConfig({
    "version": 1,
    "disable_existing_loggers": False,
    "formatters": {
        "json": {
            "()": "pythonjsonlogger.jsonlogger.JsonFormatter",
            "format": "%(timestamp)s %(level)s %(name)s %(message)s %(tenant_id)s %(operation_id)s"
        }
    },
    "handlers": {
        "console": {
            "class": "logging.StreamHandler",
            "formatter": "json"
        }
    }
})

logger = logging.getLogger(__name__)

# Always include tenant_id and operation_id
logger.info(
    "AI inference result",
    extra={
        "tenant_id": tenant_id,
        "operation_id": operation_id,
        "confidence": result["confidence"]
    }
)
```

## Testing

**Unit tests** (no async I/O, mock external calls):

```python
import pytest
from unittest.mock import AsyncMock, patch

@pytest.mark.asyncio
async def test_process_event_with_ai_succeeds():
    """Happy path: valid event -> AI decision."""
    mock_ai = AsyncMock()
    mock_ai.complete.return_value = {
        "decision": "approve",
        "confidence": 0.92
    }
    
    result = await process_event_with_ai(
        {"id": "evt-1", "type": "order", "value": 100},
        tenant_id="acme"
    )
    
    assert result["decision"] == "approve"
    mock_ai.complete.assert_called_once()

@pytest.mark.asyncio
async def test_process_event_quota_exceeded_raises():
    """Quota check fails -> QuotaExceededError."""
    with patch("check_quota_async", new_callable=AsyncMock) as mock_quota:
        mock_quota.return_value = False
        
        with pytest.raises(QuotaExceededError):
            await process_event_with_ai({"id": "evt-1"}, "acme")
```

**Integration tests** (hit real AI API + backend, use test tenant):

```python
@pytest.fixture
async def test_tenant_client():
    """Real client against test backend."""
    return BackendClient(
        base_url="http://localhost:5000",
        api_key="test-key"
    )

@pytest.mark.asyncio
async def test_fetch_and_process_end_to_end(test_tenant_client):
    """Fetch event from backend, process with AI, save result."""
    event = await test_tenant_client.fetch_event("test-tenant", "evt-123")
    ai_result = await process_event_with_ai(event, "test-tenant")
    saved = await test_tenant_client.save_ai_result("test-tenant", ai_result)
    
    assert saved["status"] == "saved"
```

## Dependencies (requirements.txt)

```
# Core async
asyncio>=3.11
httpx==0.25.*

# AI
openai==1.3.*

# Validation & serialization
pydantic==2.5.*
python-json-logger==2.0.*

# Testing
pytest==7.4.*
pytest-asyncio==0.21.*
pytest-mock==3.12.*

# Development
black==23.12.*
ruff==0.1.*
mypy==1.7.*
```

## Pre-commit Hooks

Format and lint before committing:

```bash
# Install
pip install pre-commit
pre-commit install

# Manually run
pre-commit run --all-files
```

## Naming Conventions

- **Module/package**: snake_case (ai_client, feature_extraction)
- **Classes**: PascalCase (AiClient, EventProcessor)
- **Functions/methods**: snake_case (process_event, extract_features)
- **Constants**: SCREAMING_SNAKE_CASE (MAX_RETRIES, TIMEOUT_SECONDS)
- **Async functions**: Still snake_case, no special prefix (async def complete_async() ← wrong; async def complete() ← right)
