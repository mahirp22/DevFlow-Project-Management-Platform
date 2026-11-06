import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import { User } from 'oidc-client-ts'
import type { ReactNode } from 'react'
import { expect, it, vi } from 'vitest'
import { AuthContext } from '../auth/AuthContext'
import { AttachmentPanel } from './AttachmentPanel'
import { fileContentType, sendToStorage } from './attachments'

function Wrapper({ children }: { children: ReactNode }) {
  const user = new User({ access_token: 'test-token', token_type: 'Bearer', profile: { sub: 'user', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 } })
  const auth = { user, loading: false, configured: true, error: null, signIn: vi.fn(), signOut: vi.fn(), completeSignIn: vi.fn() }
  return <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><AuthContext.Provider value={auth}><MemoryRouter>{children}</MemoryRouter></AuthContext.Provider></QueryClientProvider>
}
it('rejects unsupported empty or oversized files before uploading', () => {
  expect(fileContentType(new File(['hello'], 'notes.txt'))).toBe('text/plain')
  expect(() => fileContentType(new File(['html'], 'page.html'))).toThrow()
  expect(() => fileContentType(new File([], 'empty.txt'))).toThrow()
  expect(() => fileContentType(new File(['x'], '../notes.txt'))).toThrow()
  const oversized = new File(['x'], 'large.pdf'); Object.defineProperty(oversized, 'size', { value: 10485761 })
  expect(() => fileContentType(oversized)).toThrow('10 MiB')
})
it('uploads signed form fields with file last and never sends the Cognito bearer token to S3', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetchMock)
  await sendToStorage(new File(['hi'], 'notes.txt'), { url: 'https://storage.example.invalid', fields: { key: 'attachments/random', policy: 'test-policy' }, expiresAt: '' }, new AbortController().signal)
  const request = fetchMock.mock.calls[0][1]
  expect(request.credentials).toBe('omit')
  expect(request.headers).toBeUndefined()
  expect([...request.body.keys()]).toEqual(['key', 'policy', 'file'])
})
it('shows a setup state when storage is unconfigured', async () => {
  vi.stubGlobal('fetch', vi.fn(url => Promise.resolve(Response.json(String(url).endsWith('/configuration') ? { configured: false } : []))))
  render(<AttachmentPanel path="/tasks/t/attachments" canWrite />, { wrapper: Wrapper })
  expect(await screen.findByText('File uploads are not configured for this installation yet.')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Upload attachment' })).not.toBeInTheDocument()
})
it('lets the uploader retry finalization and refreshes the ready attachment', async () => {
  let ready = false
  const fetchMock = vi.fn((url, options) => {
    if (String(url).endsWith('/configuration')) return Promise.resolve(Response.json({ configured: true }))
    if (options.method === 'POST') { ready = true; return Promise.resolve(Response.json({ id: 'a' })) }
    return Promise.resolve(Response.json([{ id: 'a', fileName: 'notes.txt', contentType: 'text/plain', size: 12, uploader: 'Alex', ready, createdAt: '2026-11-05T00:00:00Z' }]))
  })
  vi.stubGlobal('fetch', fetchMock)
  render(<AttachmentPanel path="/tasks/t/attachments" canWrite />, { wrapper: Wrapper })
  await userEvent.click(await screen.findByRole('button', { name: 'Finish upload' }))
  expect(await screen.findByRole('button', { name: 'Download' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/api/tasks/t/attachments/a/finalize', expect.objectContaining({ method: 'POST', headers: expect.objectContaining({ Authorization: 'Bearer test-token' }) }))
})
