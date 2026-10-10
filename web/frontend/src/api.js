// Talks to the SkyBoard API. The API address comes from config.json at startup,
// next to index.html, so one build works on any host (only config.json differs).

const TOKEN_KEY = 'skyboard.token';
let baseUrl = '';

export async function loadConfig() {
  const res = await fetch('config.json', { cache: 'no-store' });
  if (!res.ok) throw new Error('config.json could not be loaded');
  const config = await res.json();
  baseUrl = String(config.apiBaseUrl || '').replace(/\/+$/, '');
}

export const apiBaseUrl = () => baseUrl;
export const imageUrl = path => (path ? baseUrl + path : '');

export function getToken() {
  try { return localStorage.getItem(TOKEN_KEY); } catch { return null; }
}

export function setToken(token) {
  try { token ? localStorage.setItem(TOKEN_KEY, token) : localStorage.removeItem(TOKEN_KEY); } catch { /* private mode */ }
}

export class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

// api('/api/posts'), api('/api/posts', { method: 'POST', body: {...} }), api('/api/images', { form })
export async function api(path, { method = 'GET', body, form } = {}) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let res;
  try {
    res = await fetch(baseUrl + path, { method, headers, body: form ?? (body !== undefined ? JSON.stringify(body) : undefined) });
  } catch {
    throw new ApiError(0, `Cannot reach the server (${baseUrl || window.location.origin})`);
  }
  if (res.status === 401 && token) {
    setToken(null);
    window.dispatchEvent(new Event('skyboard:logout'));   // AuthProvider shows the login page
  }
  if (res.status === 204) return null;
  const data = await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(res.status, errorMessage(data, res.status));
  return data;
}

// FastAPI errors: {"detail": "text"} or, for invalid input, {"detail": [{"loc": [...], "msg": "..."}]}.
function errorMessage(data, status) {
  const detail = data?.detail;
  if (typeof detail === 'string') return detail;
  if (Array.isArray(detail) && detail.length) {
    const first = detail[0];
    const field = first.loc?.filter(part => part !== 'body').join('.');
    return field ? `${field}: ${first.msg}` : first.msg;
  }
  return `Request failed (${status})`;
}
