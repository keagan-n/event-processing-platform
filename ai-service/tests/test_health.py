from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health_returns_healthy_with_active_provider():
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "healthy", "provider": "stub"}
