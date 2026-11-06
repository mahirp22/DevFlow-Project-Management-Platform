export class ApiError extends Error {
  readonly status: number
  readonly traceId?: string

  constructor(status: number, message: string, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.traceId = traceId
  }
}

// Public build-time configuration. Prefer same-origin /api in production.
const baseUrl = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '')

export function getJson(path: string, signal?: AbortSignal, token?: string): Promise<unknown> {
  return requestJson(path, { signal, token })
}

export async function requestJson(path: string, options: { method?: 'GET' | 'POST' | 'PUT' | 'DELETE'; signal?: AbortSignal; token?: string; body?: unknown } = {}): Promise<unknown> {
  const { signal, token, method = 'GET' } = options
  const timeout = AbortSignal.timeout(8_000)
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: { Accept: 'application/json', ...(options.body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    cache: 'no-store',
    signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
  })
  if (response.status === 204) return undefined
  const body: unknown = await response.json().catch(() => null)
  if (!response.ok) {
    const problem = body !== null && typeof body === 'object' ? body : {}
    const title = 'title' in problem && typeof problem.title === 'string'
      ? problem.title : `Request failed (${response.status}).`
    const traceId = 'traceId' in problem && typeof problem.traceId === 'string'
      ? problem.traceId : undefined
    throw new ApiError(response.status, title, traceId)
  }
  if (body === null) throw new Error('The API returned an invalid response.')
  return body
}
