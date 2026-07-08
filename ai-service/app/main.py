"""AI Service - FastAPI application.

Analyzes job payloads on behalf of the .NET worker. The active provider is
selected from the environment (see app/config.py); the default is a free,
deterministic stub that requires no API keys.
"""

import logging
import time

from fastapi import FastAPI

from app.config import create_provider
from app.models import AnalyzeRequest, AnalyzeResponse, HealthResponse

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

app = FastAPI(title="AI Service", version="1.0.0")

provider = create_provider()
logger.info("AI service starting with provider '%s'", provider.name)


@app.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    return HealthResponse(status="healthy", provider=provider.name)


@app.post("/v1/analyze", response_model=AnalyzeResponse)
async def analyze(request: AnalyzeRequest) -> AnalyzeResponse:
    started = time.perf_counter()
    summary, model_name = await provider.analyze(
        request.job_type, request.payload
    )
    latency_ms = int((time.perf_counter() - started) * 1000)
    logger.info(
        "Analyzed job %s (type=%s, model=%s, latency_ms=%d)",
        request.job_id,
        request.job_type,
        model_name,
        latency_ms,
    )
    return AnalyzeResponse(
        job_id=request.job_id,
        summary=summary,
        model=model_name,
        latency_ms=latency_ms,
    )
