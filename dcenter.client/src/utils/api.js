import { SUPERVISOR_HEADER } from '@/utils/constants';

const defaults = { baseURL: '/api', headers: {}, onUnauthorized: null };
const TIMEOUT_MS = 60000;
const DOWNLOAD_TIMEOUT_MS = 120000;
const TIMEOUT_MESSAGE = 'The server did not respond in time. Check today\'s entries before trying again, in case it was saved.';

function buildUrl(url, params) {
  const target = new URL(defaults.baseURL + url, window.location.origin);
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== '') {
        target.searchParams.set(key, value);
      }
    }
  }
  return target.pathname + target.search;
}

async function parseBody(res, responseType) {
  if (responseType === 'blob') return res.blob();
  const contentType = res.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    const text = await res.text();
    return text ? JSON.parse(text) : null;
  }
  return res.text();
}

async function request(method, url, { params, body, headers, responseType, timeoutMs } = {}) {
  const finalHeaders = { ...defaults.headers, ...headers };
  const isFormData = body instanceof FormData;
  let payload = body;

  if (body !== undefined && !isFormData) {
    finalHeaders['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  } else if (isFormData) {
    delete finalHeaders['Content-Type'];
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs ?? (responseType === 'blob' ? DOWNLOAD_TIMEOUT_MS : TIMEOUT_MS));
  let res;
  let data;
  try {
    const init = { method, headers: finalHeaders, cache: 'no-store', signal: controller.signal };
    if (payload !== undefined) init.body = payload;
    res = await fetch(buildUrl(url, params), init);
    data = res.status === 204 ? null : await parseBody(res, res.ok ? responseType : undefined);
  } catch (e) {
    if (e?.name !== 'AbortError') throw e;
    const error = new Error(`Request to ${url} timed out`);
    error.response = { status: 0, data: TIMEOUT_MESSAGE };
    throw error;
  } finally {
    clearTimeout(timer);
  }

  if (res.status === 401 && finalHeaders[SUPERVISOR_HEADER] && url !== '/supervisor/login') defaults.onUnauthorized?.();

  if (!res.ok) {
    const error = new Error(`Request to ${url} failed with status ${res.status}`);
    error.response = { status: res.status, data };
    throw error;
  }
  return { data, status: res.status };
}

const api = {
  get: (url, opts) => request('GET', url, opts),
  post: (url, body, opts = {}) => request('POST', url, { ...opts, body }),
  put: (url, body, opts = {}) => request('PUT', url, { ...opts, body }),
  delete: (url, opts) => request('DELETE', url, opts),
  defaults,
};

export default api;
