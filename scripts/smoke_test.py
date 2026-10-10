"""Exercise the running Compose demo using only Python's standard library.

Creates jobs under unique tenant IDs. --broker-outage also stops/restarts
RabbitMQ in the selected Compose project; use a local demo stack.
"""

import argparse
import base64
import json
import subprocess
import time
import uuid
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def request(url, data=None, authorization=None):
    headers = {"Content-Type": "application/json"}
    if authorization:
        headers["Authorization"] = authorization
    body = None if data is None else json.dumps(data).encode()
    req = Request(url, data=body, headers=headers)
    try:
        response = urlopen(req, timeout=10)
    except HTTPError as error:
        response = error
    with response:
        raw = response.read()
        try:
            body = json.loads(raw) if raw else None
        except (json.JSONDecodeError, UnicodeDecodeError):
            body = None
        return response.status, response.headers, body


def wait_for(check, description, timeout):
    deadline = time.monotonic() + timeout
    last_error = None
    while time.monotonic() < deadline:
        try:
            value = check()
            if value:
                return value
        except (URLError, TimeoutError, OSError) as error:
            last_error = error
        time.sleep(0.5)
    raise AssertionError(f"Timed out waiting for {description}. Last error: {last_error}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://localhost:5200")
    parser.add_argument("--rabbit-url", default="http://localhost:15672")
    parser.add_argument("--queue", default="jobs.submitted")
    parser.add_argument("--dlq", default="jobs.submitted.dlq")
    parser.add_argument("--tokens", type=int, default=10)
    parser.add_argument("--timeout", type=int, default=90)
    parser.add_argument("--broker-outage", action="store_true")
    parser.add_argument("--compose-project")
    parser.add_argument("--compose-file", action="append", default=[])
    parser.add_argument("--env-file")
    args = parser.parse_args()
    base = args.base_url.rstrip("/")
    run_id = uuid.uuid4().hex[:12]
    authorization = "Basic " + base64.b64encode(b"guest:guest").decode()
    compose = ["docker", "compose"]
    if args.env_file:
        compose += ["--env-file", args.env_file]
    if args.compose_project:
        compose += ["-p", args.compose_project]
    for path in args.compose_file:
        compose += ["-f", path]

    def submit(tenant, payload):
        return request(base + "/jobs", {"tenantId": tenant, "jobType": "analysis", "payload": json.dumps(payload)})

    def terminal(job_id):
        status, _, body = request(f"{base}/jobs/{job_id}/status")
        require(status == 200, f"Status request returned {status}: {body}")
        return body if body["status"] in ("Completed", "Failed") else None

    def queue_state(name):
        status, _, body = request(args.rabbit_url.rstrip("/") + "/api/queues/%2F/" + quote(name, safe=""), authorization=authorization)
        require(status in (200, 404), f"RabbitMQ management returned {status}")
        return body if status == 200 else {}

    wait_for(lambda: request(base + "/health")[0] == 200, "API health", args.timeout)
    status, _, body = request(base + "/jobs", {"tenantId": "", "jobType": "", "payload": ""})
    require(status == 400 and len(body["errors"]) == 3, f"Invalid input was not rejected: {status}, {body}")
    status, _, _ = request(f"{base}/jobs/{uuid.uuid4()}/status")
    require(status == 404, "Unknown job should return 404")
    status, _, _ = submit("x" * 256, {"text": "invalid tenant"})
    require(status == 400, "Oversized tenant should return 400")
    status, _, _ = submit(f"smoke-size-{run_id}", {"text": "x" * 65537})
    require(status == 400, "Oversized payload should return 400")
    status, _, _ = submit(f"smoke-body-{run_id}", {"text": "x" * (300 * 1024)})
    require(status == 413, "Oversized HTTP body should return 413")
    print("PASS validation, payload/body limits, and missing-job contracts", flush=True)

    status, headers, job = submit(f"smoke-happy-{run_id}", {"text": "quarterly portfolio risk review"})
    require(status == 201 and job["status"] == "Pending", f"Submit failed: {status}, {job}")
    require(headers["Location"] == f'/jobs/{job["jobId"]}/status', "Location must point to the status route")
    result = wait_for(lambda: terminal(job["jobId"]), "successful analysis", args.timeout)
    require(result["status"] == "Completed" and result["result"] and result["attemptCount"] == 0, f"Unexpected result: {result}")
    print(f'PASS persisted job -> outbox -> RabbitMQ -> Python -> Completed ({job["jobId"]})', flush=True)

    baseline = queue_state(args.dlq).get("messages", 0)
    status, _, job = submit(f"smoke-failure-{run_id}", {"forceFailure": True})
    require(status == 201, "Failure-demo job submission failed")
    result = wait_for(lambda: terminal(job["jobId"]), "retry exhaustion", args.timeout)
    require(result["status"] == "Failed" and result["attemptCount"] == 2 and result["failureReason"], f"Unexpected failure state: {result}")
    wait_for(lambda: queue_state(args.dlq).get("messages", 0) > baseline, "dead-letter message", args.timeout)
    print("PASS three job attempts -> Failed with reason -> dead-letter queue", flush=True)

    tenant = f"smoke-burst-{run_id}"
    for _ in range(args.tokens):
        require(submit(tenant, {"text": "burst"})[0] == 201, "Rate limiter rejected within budget")
    status, headers, _ = submit(tenant, {"text": "over budget"})
    require(status == 429 and int(headers["Retry-After"]) > 0, "Expected 429 with Retry-After")
    require(submit(f"smoke-isolated-{run_id}", {"text": "another tenant"})[0] == 201, "Tenant limits are not isolated")
    print("PASS per-tenant budget, 429/Retry-After, tenant isolation", flush=True)

    if args.broker_outage:
        # Always restore the broker, including when a check fails.
        try:
            subprocess.run(compose + ["stop", "rabbitmq"], check=True)
            status, _, job = submit(f"smoke-outbox-{run_id}", {"text": "accepted during broker outage"})
            require(status == 201, "Outbox should accept jobs while RabbitMQ is stopped")
            status, _, body = request(f'{base}/jobs/{job["jobId"]}/status')
            require(status == 200 and body["status"] == "Pending", "Outage job should remain Pending")
        finally:
            subprocess.run(compose + ["start", "rabbitmq"], check=True)
        result = wait_for(lambda: terminal(job["jobId"]), "outbox recovery after broker restart", args.timeout)
        require(result["status"] == "Completed", f"Outbox recovery failed: {result}")
        print("PASS broker outage: 201/Pending -> restart -> confirmed dispatch -> Completed", flush=True)

    # Management statistics can lag consumer recovery after a broker restart.
    def drained_queue_with_consumer():
        state = queue_state(args.queue)
        return state.get("messages") == 0 and state.get("consumers", 0) >= 1

    wait_for(drained_queue_with_consumer, "main queue drain and worker recovery", args.timeout)
    print("All smoke checks passed.", flush=True)


if __name__ == "__main__":
    main()
