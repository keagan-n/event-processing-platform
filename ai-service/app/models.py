"""Pydantic models for the AI service HTTP contract."""

from pydantic import BaseModel


class AnalyzeRequest(BaseModel):
    """Request body for POST /v1/analyze."""

    job_id: str
    job_type: str
    payload: str


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
