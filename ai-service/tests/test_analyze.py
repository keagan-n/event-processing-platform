from fastapi.testclient import TestClient

from app.main import app
from app.providers.stub import MODEL_NAME

client = TestClient(app)

VALID_REQUEST = {
    "job_id": "7f9c9a5e-0000-0000-0000-000000000001",
    "job_type": "report",
    "payload": "Generate the quarterly revenue report for tenant acme.",
}


def test_analyze_happy_path():
    response = client.post("/v1/analyze", json=VALID_REQUEST)

    assert response.status_code == 200
    body = response.json()
    assert body["job_id"] == VALID_REQUEST["job_id"]
    assert isinstance(body["summary"], str)
    assert body["summary"]
    assert "report" in body["summary"]
    assert body["model"] == MODEL_NAME
    assert isinstance(body["latency_ms"], int)
    assert body["latency_ms"] >= 150  # stub simulates ~200ms of latency


def test_analyze_is_deterministic_for_same_payload():
    first = client.post("/v1/analyze", json=VALID_REQUEST)
    second = client.post("/v1/analyze", json=VALID_REQUEST)

    assert first.status_code == 200
    assert second.status_code == 200
    assert first.json()["summary"] == second.json()["summary"]
    assert first.json()["model"] == second.json()["model"]


def test_analyze_missing_field_returns_422():
    response = client.post(
        "/v1/analyze",
        json={"job_id": "abc", "job_type": "report"},  # payload missing
    )

    assert response.status_code == 422


def test_analyze_wrong_type_returns_422():
    response = client.post(
        "/v1/analyze",
        json={"job_id": "abc", "job_type": "report", "payload": 12345},
    )

    assert response.status_code == 422
