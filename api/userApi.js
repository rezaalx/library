/**
 * UserApi: Minimal REST client for User resources using fetch.
 *
 * Methods:
 *  - listUsers({ page, perPage, filters })
 *  - getUser(userId)
 *  - createUser(user)
 *  - updateUser(userId, user)
 *  - deleteUser(userId)
 *
 * Requires a global `fetch` (Node 18+ or browsers). For older Node, install `node-fetch` and set `global.fetch`.
 */

class ApiError extends Error {
  constructor(status, message, body) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

export class UserApi {
  constructor(baseUrl, options = {}) {
    if (!baseUrl) throw new Error('baseUrl is required');
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.authToken = options.authToken || null;
    this.defaultHeaders = Object.assign({}, options.defaultHeaders || {});
    this.timeoutMs = options.timeoutMs != null ? options.timeoutMs : 30000;
  }

  _buildUrl(path, query) {
    const fullPath = path.startsWith('/') ? path : `/${path}`;
    const url = new URL(`${this.baseUrl}${fullPath}`);
    if (query && typeof query === 'object') {
      for (const [key, value] of Object.entries(query)) {
        if (value === undefined || value === null) continue;
        if (Array.isArray(value)) {
          for (const v of value) url.searchParams.append(key, String(v));
        } else {
          url.searchParams.set(key, String(value));
        }
      }
    }
    return url.toString();
  }

  _buildHeaders(extra) {
    const headers = Object.assign({}, this.defaultHeaders);
    if (this.authToken) headers['Authorization'] = `Bearer ${this.authToken}`;
    if (extra) Object.assign(headers, extra);
    return headers;
  }

  async _request(method, path, { query, body, headers } = {}) {
    if (typeof fetch !== 'function') {
      throw new Error('global fetch is not available. Use Node 18+ or polyfill fetch.');
    }
    const url = this._buildUrl(path, query);
    const finalHeaders = this._buildHeaders(headers);
    const controller = new AbortController();
    const id = setTimeout(() => controller.abort(), this.timeoutMs);
    const options = {
      method: method.toUpperCase(),
      headers: finalHeaders,
      signal: controller.signal,
    };
    if (body != null) {
      options.body = JSON.stringify(body);
      if (!options.headers['Content-Type']) options.headers['Content-Type'] = 'application/json';
      if (!options.headers['Accept']) options.headers['Accept'] = 'application/json';
    }

    let response;
    try {
      response = await fetch(url, options);
    } catch (err) {
      clearTimeout(id);
      if (err && err.name === 'AbortError') {
        throw new Error(`Request timed out after ${this.timeoutMs} ms: ${method.toUpperCase()} ${url}`);
      }
      throw err;
    }
    clearTimeout(id);

    const contentType = response.headers.get('content-type') || '';
    const isJson = contentType.includes('application/json');
    const text = await response.text();
    const data = isJson && text ? safeParseJson(text) : (text ? { text } : null);

    if (!response.ok) {
      throw new ApiError(response.status, `HTTP ${response.status} for ${method.toUpperCase()} ${url}`, text);
    }
    return { status: response.status, data, headers: headerEntriesToObject(response.headers) };
  }

  // Public API
  async listUsers({ page, perPage, filters } = {}) {
    const query = Object.assign({},
      page != null ? { page } : {},
      perPage != null ? { perPage } : {},
      filters || {}
    );
    const { data } = await this._request('GET', '/users', { query });
    return data || {};
  }

  async getUser(userId) {
    if (userId == null) throw new Error('userId is required');
    const { data } = await this._request('GET', `/users/${userId}`);
    return data || {};
  }

  async createUser(user) {
    if (!user || typeof user !== 'object') throw new Error('user object is required');
    const { data } = await this._request('POST', '/users', { body: user });
    return data || {};
  }

  async updateUser(userId, user) {
    if (userId == null) throw new Error('userId is required');
    if (!user || typeof user !== 'object') throw new Error('user object is required');
    const { data } = await this._request('PUT', `/users/${userId}`, { body: user });
    return data || {};
  }

  async deleteUser(userId) {
    if (userId == null) throw new Error('userId is required');
    await this._request('DELETE', `/users/${userId}`);
    return true;
  }
}

function safeParseJson(text) {
  try { return JSON.parse(text); } catch { return null; }
}

function headerEntriesToObject(headers) {
  const obj = {};
  for (const [k, v] of headers.entries()) obj[k] = v;
  return obj;
}

export default UserApi;

