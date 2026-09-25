const defaults = { baseURL: '/api', headers: {}, onUnauthorized: null };
const SUPERVISOR_HEADER = 'X-Supervisor-Token';

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

async function request(method, url, { params, body, headers, responseType } = {}) {
  const finalHeaders = { ...defaults.headers, ...(headers ?? {}) };
  const isFormData = body instanceof FormData;
  let payload = body;

  if (body !== undefined && !isFormData) {
    finalHeaders['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  } else if (isFormData) {
    delete finalHeaders['Content-Type'];
  }

  const res = await fetch(buildUrl(url, params), { method, headers: finalHeaders, body: payload, cache: 'no-store' });
  const data = res.status === 204 ? null : await parseBody(res, res.ok ? responseType : undefined);

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
