"""JSON-lines bridge to a persistent HTTP client. No DOM, JavaScript or browser runtime."""
import json
import sys
from urllib.parse import urlsplit

from curl_cffi import requests


def allowed_url(url):
    try:
        parsed = urlsplit(url)
        return (parsed.scheme == "https" and parsed.hostname in ("www.futbin.com", "futbin.com")
                and parsed.port in (None, 443) and parsed.username is None and parsed.password is None)
    except (ValueError, TypeError):
        return False


def create_session(config):
    proxy = config.get("proxy") or {}
    return requests.Session(
        impersonate="safari",  # TLS/HTTP fingerprint only; no Safari installation or process.
        timeout=config["timeout"], verify=True, trust_env=False, allow_redirects=False,
        # Empty proxy explicitly disables libcurl's environment proxy fallback.
        proxy=proxy.get("server", ""),
        proxy_auth=(proxy["username"], proxy.get("password") or "") if proxy.get("username") else None,
        headers={"Accept-Language": "en-GB,en;q=0.9"},
    )


def fetch(session, url):
    if not allowed_url(url):
        return {"error": "invalid_url"}
    # Redirect targets are validated and rate-limited by the C# caller before the next request.
    response = session.get(url)
    return {"status": response.status_code, "finalUri": str(response.url), "html": response.text,
            "retryAfter": response.headers.get("retry-after"), "location": response.headers.get("location"),
            "mitigation": response.headers.get("cf-mitigated"), "rayId": response.headers.get("cf-ray")}


def emit(value):
    print(json.dumps(value, ensure_ascii=True, separators=(",", ":")), flush=True)


def main():
    session = None
    try:
        config = json.loads(sys.stdin.readline())
        session = create_session(config)
        emit({"ready": True})
        for line in sys.stdin:
            try:
                emit(fetch(session, json.loads(line)["url"]))
            except Exception:
                # Exception strings may include proxy URLs/passwords. Never return or log them.
                emit({"error": "request_failed"})
    except Exception:
        emit({"error": "initialization_failed"})
    finally:
        if session is not None:
            session.close()


if __name__ == "__main__":
    main()
