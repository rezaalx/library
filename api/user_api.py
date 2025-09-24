"""
UserApi: Minimal REST client for User resources.

Provides CRUD methods against a typical REST backend:
  - GET    /users
  - GET    /users/{id}
  - POST   /users
  - PUT    /users/{id}
  - DELETE /users/{id}

Standard library only (urllib), no external dependencies.
"""

from __future__ import annotations

import json
import ssl
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Any, Dict, Iterable, Mapping, Optional, Tuple


def _merge_query_params(
    base_params: Optional[Mapping[str, Any]],
    extra_params: Optional[Mapping[str, Any]],
) -> Dict[str, Any]:
    merged: Dict[str, Any] = {}
    if base_params:
        merged.update({k: v for k, v in base_params.items() if v is not None})
    if extra_params:
        merged.update({k: v for k, v in extra_params.items() if v is not None})
    return merged


@dataclass
class UserApiConfig:
    base_url: str
    auth_token: Optional[str] = None
    timeout_seconds: int = 30
    default_headers: Optional[Mapping[str, str]] = None
    verify_ssl: bool = True


class ApiError(RuntimeError):
    def __init__(self, status: int, message: str, body: Optional[str] = None):
        super().__init__(message)
        self.status = status
        self.body = body


class UserApi:
    def __init__(self, config: UserApiConfig):
        self._base_url = config.base_url.rstrip("/")
        self._auth_token = config.auth_token
        self._timeout = config.timeout_seconds
        self._default_headers = dict(config.default_headers or {})
        self._verify_ssl = config.verify_ssl

    def _build_url(self, path: str, query: Optional[Mapping[str, Any]] = None) -> str:
        if not path.startswith("/"):
            path = "/" + path
        url = f"{self._base_url}{path}"
        if query:
            # Filter out None values and encode
            query_no_none = {k: v for k, v in query.items() if v is not None}
            qs = urllib.parse.urlencode(query_no_none, doseq=True)
            if qs:
                url = f"{url}?{qs}"
        return url

    def _build_headers(self, extra: Optional[Mapping[str, str]] = None) -> Dict[str, str]:
        headers: Dict[str, str] = {}
        headers.update(self._default_headers)
        if self._auth_token:
            headers.setdefault("Authorization", f"Bearer {self._auth_token}")
        if extra:
            headers.update(extra)
        return headers

    def _get_ssl_context(self) -> Optional[ssl.SSLContext]:
        if self._verify_ssl:
            return None
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        return ctx

    def _request(
        self,
        method: str,
        path: str,
        *,
        query: Optional[Mapping[str, Any]] = None,
        body: Optional[Mapping[str, Any]] = None,
        headers: Optional[Mapping[str, str]] = None,
    ) -> Tuple[int, Dict[str, Any] | None, Mapping[str, str]]:
        url = self._build_url(path, query)
        final_headers = self._build_headers(headers)
        data_bytes: Optional[bytes] = None
        if body is not None:
            data_bytes = json.dumps(body).encode("utf-8")
            final_headers.setdefault("Content-Type", "application/json")
            final_headers.setdefault("Accept", "application/json")

        req = urllib.request.Request(url=url, method=method.upper())
        for hk, hv in final_headers.items():
            req.add_header(hk, hv)

        ssl_context = self._get_ssl_context()

        try:
            with urllib.request.urlopen(
                req, data=data_bytes, timeout=self._timeout, context=ssl_context
            ) as resp:
                status: int = resp.getcode()
                resp_headers = {k: v for k, v in resp.headers.items()}
                content_type = resp_headers.get("Content-Type", "")
                raw = resp.read()
                if not raw:
                    return status, None, resp_headers
                if "application/json" in content_type:
                    try:
                        parsed = json.loads(raw.decode("utf-8"))
                    except Exception:
                        parsed = None
                    return status, parsed, resp_headers
                # Fallback: return text payload in a JSON envelope
                return status, {"text": raw.decode("utf-8", errors="replace")}, resp_headers
        except urllib.error.HTTPError as e:
            status = e.code
            try:
                body_text = e.read().decode("utf-8") if e.fp else None
            except Exception:
                body_text = None
            message = f"HTTP {status} for {method.upper()} {url}"
            raise ApiError(status=status, message=message, body=body_text)
        except urllib.error.URLError as e:
            raise RuntimeError(f"Network error for {method.upper()} {url}: {e}")

    # Public API methods

    def list_users(
        self,
        *,
        page: Optional[int] = None,
        per_page: Optional[int] = None,
        filters: Optional[Mapping[str, Any]] = None,
    ) -> Dict[str, Any] | Iterable[Dict[str, Any]]:
        query = _merge_query_params({"page": page, "per_page": per_page}, filters)
        status, payload, _ = self._request("GET", "/users", query=query)
        if status >= 200 and status < 300:
            return payload or {}
        raise ApiError(status, f"Unexpected status {status} for list_users", json.dumps(payload))

    def get_user(self, user_id: str | int) -> Dict[str, Any]:
        status, payload, _ = self._request("GET", f"/users/{user_id}")
        if status >= 200 and status < 300:
            return payload or {}
        raise ApiError(status, f"Unexpected status {status} for get_user", json.dumps(payload))

    def create_user(self, user_data: Mapping[str, Any]) -> Dict[str, Any]:
        status, payload, _ = self._request("POST", "/users", body=dict(user_data))
        if status >= 200 and status < 300:
            return payload or {}
        raise ApiError(status, f"Unexpected status {status} for create_user", json.dumps(payload))

    def update_user(self, user_id: str | int, user_data: Mapping[str, Any]) -> Dict[str, Any]:
        status, payload, _ = self._request("PUT", f"/users/{user_id}", body=dict(user_data))
        if status >= 200 and status < 300:
            return payload or {}
        raise ApiError(status, f"Unexpected status {status} for update_user", json.dumps(payload))

    def delete_user(self, user_id: str | int) -> bool:
        status, payload, _ = self._request("DELETE", f"/users/{user_id}")
        if status == 204 or (status >= 200 and status < 300):
            return True
        raise ApiError(status, f"Unexpected status {status} for delete_user", json.dumps(payload))


__all__ = [
    "UserApi",
    "UserApiConfig",
    "ApiError",
]

