from app.config import create_provider, get_provider_name
from app.providers.anthropic_provider import AnthropicProvider
from app.providers.stub import StubProvider


def test_defaults_to_stub_when_env_unset(monkeypatch):
    monkeypatch.delenv("AI_PROVIDER", raising=False)
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)

    assert get_provider_name() == "stub"
    assert isinstance(create_provider(), StubProvider)


def test_unknown_provider_falls_back_to_stub(monkeypatch):
    monkeypatch.setenv("AI_PROVIDER", "something-else")

    assert get_provider_name() == "stub"
    assert isinstance(create_provider(), StubProvider)


def test_anthropic_without_api_key_falls_back_to_stub(monkeypatch):
    monkeypatch.setenv("AI_PROVIDER", "anthropic")
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)

    assert get_provider_name() == "stub"
    assert isinstance(create_provider(), StubProvider)


def test_anthropic_selected_when_configured(monkeypatch):
    monkeypatch.setenv("AI_PROVIDER", "anthropic")
    monkeypatch.setenv("ANTHROPIC_API_KEY", "test-key")

    assert get_provider_name() == "anthropic"
    assert isinstance(create_provider(), AnthropicProvider)
