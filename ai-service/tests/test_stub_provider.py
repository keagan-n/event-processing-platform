import pytest

from app.providers.stub import MODEL_NAME, StubProvider, build_summary


@pytest.mark.asyncio
async def test_stub_analyze_is_deterministic():
    provider = StubProvider()

    first = await provider.analyze("report", "process orders for tenant acme")
    second = await provider.analyze("report", "process orders for tenant acme")

    assert first == second
    assert first[1] == MODEL_NAME


def test_build_summary_includes_length_and_keywords():
    payload = "orders orders shipping"
    summary = build_summary("report", payload)

    assert "report" in summary
    assert f"{len(payload)} characters" in summary
    assert "3 words" in summary
    assert "orders" in summary


def test_build_summary_handles_payload_without_keywords():
    summary = build_summary("noop", "a an of")

    assert "noop" in summary
    assert "Top keywords" not in summary
