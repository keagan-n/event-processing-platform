"""Anthropic-backed analysis provider.

Optional provider, used only when AI_PROVIDER=anthropic and ANTHROPIC_API_KEY
is set. The anthropic SDK is imported lazily inside the provider so the
default stub path never requires it to be configured.
"""

from typing import Any, Tuple

MODEL_NAME = "claude-haiku-4-5"

_PROMPT = "Summarize this job payload in 2 sentences."
_TIMEOUT_SECONDS = 30.0
_MAX_TOKENS = 300


class AnthropicProvider:
    """Analysis provider backed by the Anthropic Messages API."""

    name = "anthropic"

    def __init__(self, api_key: str) -> None:
        self._api_key = api_key
        self._client: Any = None

    def _get_client(self) -> Any:
        if self._client is None:
            # Lazy import: the stub path must work without the anthropic
            # SDK being configured.
            from anthropic import AsyncAnthropic

            self._client = AsyncAnthropic(
                api_key=self._api_key,
                timeout=_TIMEOUT_SECONDS,
            )
        return self._client

    async def analyze(self, job_type: str, payload: str) -> Tuple[str, str]:
        client = self._get_client()
        message = await client.messages.create(
            model=MODEL_NAME,
            max_tokens=_MAX_TOKENS,
            messages=[
                {
                    "role": "user",
                    "content": (
                        f"{_PROMPT}\n\n"
                        f"Job type: {job_type}\n\n"
                        f"Payload:\n{payload}"
                    ),
                }
            ],
        )
        summary = "".join(
            block.text for block in message.content if block.type == "text"
        )
        return summary, MODEL_NAME
