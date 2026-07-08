"""Deterministic stub analysis provider.

Default provider. Derives a summary from the payload itself (length stats and
top keywords) so the same input always produces the same output. No network
calls and no API keys required. A small fixed latency keeps demos realistic.
"""

import asyncio
import re
from collections import Counter
from typing import List, Tuple

MODEL_NAME = "stub-analyzer-v1"

_LATENCY_SECONDS = 0.2

_STOP_WORDS = frozenset(
    {
        "a",
        "an",
        "and",
        "are",
        "as",
        "at",
        "be",
        "but",
        "by",
        "for",
        "from",
        "has",
        "have",
        "in",
        "is",
        "it",
        "its",
        "of",
        "on",
        "or",
        "that",
        "the",
        "this",
        "to",
        "was",
        "were",
        "will",
        "with",
    }
)


def _extract_keywords(payload: str, limit: int = 3) -> List[str]:
    words = re.findall(r"[a-z0-9]+", payload.lower())
    keywords = [w for w in words if len(w) > 2 and w not in _STOP_WORDS]
    counts = Counter(keywords)
    # Counter.most_common preserves first-seen order for ties, so the
    # result is deterministic for identical input.
    return [word for word, _ in counts.most_common(limit)]


def build_summary(job_type: str, payload: str) -> str:
    """Build a deterministic summary from job type and payload."""
    word_count = len(re.findall(r"\S+", payload))
    keywords = _extract_keywords(payload)
    summary = (
        f"Job of type '{job_type}' analyzed: payload contains "
        f"{len(payload)} characters and {word_count} words."
    )
    if keywords:
        summary += f" Top keywords: {', '.join(keywords)}."
    return summary


class StubProvider:
    """Deterministic, offline analysis provider."""

    name = "stub"

    async def analyze(self, job_type: str, payload: str) -> Tuple[str, str]:
        await asyncio.sleep(_LATENCY_SECONDS)
        return build_summary(job_type, payload), MODEL_NAME
