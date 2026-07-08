"""Provider abstraction for job payload analysis."""

from typing import Protocol, Tuple


class AnalysisProvider(Protocol):
    """Protocol every analysis provider must implement."""

    name: str

    async def analyze(self, job_type: str, payload: str) -> Tuple[str, str]:
        """Analyze a job payload.

        Returns a tuple of (summary, model_name).
        """
        ...
