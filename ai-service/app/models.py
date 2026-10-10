"""Pydantic models for the AI service HTTP contract."""

from pydantic import BaseModel, Field


class AnalyzeRequest(BaseModel):
    """Request body for POST /v1/analyze."""

    job_id: str = Field(min_length=1, max_length=128)
    job_type: str = Field(min_length=1, max_length=255)
    payload: str = Field(min_length=1, max_length=65536)


class AnalyzeResponse(BaseModel):
    """Response body for POST /v1/analyze."""

    job_id: str
    summary: str
    model: str
    latency_ms: int


class HealthResponse(BaseModel):
    """Response body for GET /health."""

    status: str
    provider: str
