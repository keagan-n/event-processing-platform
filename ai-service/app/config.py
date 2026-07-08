"""Provider selection from environment variables.

Defaults to the free deterministic stub provider. The anthropic provider is
selected only when AI_PROVIDER=anthropic AND ANTHROPIC_API_KEY is set, so the
default path requires zero API keys.
"""

import os

from app.providers.base import AnalysisProvider

DEFAULT_PROVIDER = "stub"


def get_provider_name() -> str:
    """Resolve the active provider name from the environment."""
    requested = os.environ.get("AI_PROVIDER", DEFAULT_PROVIDER).strip().lower()
    if requested == "anthropic" and os.environ.get("ANTHROPIC_API_KEY"):
        return "anthropic"
    return DEFAULT_PROVIDER


def create_provider() -> AnalysisProvider:
    """Create the active analysis provider."""
    if get_provider_name() == "anthropic":
        from app.providers.anthropic_provider import AnthropicProvider

        return AnthropicProvider(api_key=os.environ["ANTHROPIC_API_KEY"])

    from app.providers.stub import StubProvider

    return StubProvider()
