import { describe, expect, it, vi } from 'vitest'
import { ApiError, getJson } from './api'

describe('API client', () => {
  it('preserves HTTP status and safe problem details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ title: 'Conflict', traceId: 'abc' }, { status: 409 })))
    await expect(getJson('/example')).rejects.toMatchObject({ status: 409, message: 'Conflict', traceId: 'abc' })
  })
  it('handles non-JSON errors without displaying server HTML', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>Error</html>', { status: 502 })))
    await expect(getJson('/example')).rejects.toEqual(new ApiError(502, 'Request failed (502).'))
  })
  it('rejects malformed successful JSON', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('invalid')))
    await expect(getJson('/example')).rejects.toThrow('invalid response')
  })
  it('propagates network failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    await expect(getJson('/example')).rejects.toThrow('Failed to fetch')
  })
  it('combines caller cancellation with the request timeout', async () => {
    const controller = new AbortController()
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ status: 'Healthy' }))
    vi.stubGlobal('fetch', fetchMock)
    await getJson('/health', controller.signal)
    const signal = fetchMock.mock.calls[0][1].signal as AbortSignal
    expect(signal.aborted).toBe(false)
    controller.abort()
    expect(signal.aborted).toBe(true)
  })
})
