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

export async function getJson(path: string, signal?: AbortSignal): Promise<unknown> {
  const timeout = AbortSignal.timeout(8_000)
  const response = await fetch(`${baseUrl}${path}`, {
    headers: { Accept: 'application/json' },
    cache: 'no-store',
    signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
  })
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