# AI Service Dockerfile
# Local-demo runtime; development tooling is installed separately.

FROM python:3.11-alpine@sha256:d9368b3a5ac59afea7b5d4f2e2aea0941dbf9fdee9c369c5bec00b98244bc929

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1

RUN apk upgrade --no-cache

WORKDIR /app

# Install dependencies
COPY requirements.txt .
RUN python -m pip install --no-cache-dir --upgrade pip==26.2.1 setuptools==84.0.0 wheel==0.48.0 \
    && python -m pip install --no-cache-dir -r requirements.txt \
    && python -m pip uninstall --yes setuptools wheel pip \
    && python -c "import shutil; shutil.rmtree('/usr/local/lib/python3.11/ensurepip')"

# Copy application code
COPY app/ ./app/

RUN addgroup -S -g 10001 app && adduser -S -D -H -u 10001 -G app app
USER 10001:10001

# Health check (urllib.request is in the standard library)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health', timeout=5)" || exit 1

# Run service
CMD ["python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
