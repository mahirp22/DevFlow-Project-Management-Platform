import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, useLocation } from 'react-router'
import { User } from 'oidc-client-ts'
import type { ReactNode } from 'react'
import { beforeEach, expect, it, vi } from 'vitest'
import { AuthContext } from '../auth/AuthContext'
import { useProjectLive } from './useProjectLive'

const mock = vi.hoisted(() => ({
  handlers: {} as Record<string, () => void>,
  start: vi.fn(), stop: vi.fn(), invoke: vi.fn(),
}))
vi.mock('@microsoft/signalr', () => ({
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    configureLogging() { return this }
    withAutomaticReconnect() { return this }
    build() { return { start: mock.start, stop: mock.stop, invoke: mock.invoke,
      on: (event: string, callback: () => void) => { mock.handlers[event] = callback },
      onreconnecting: (callback: () => void) => { mock.handlers.reconnecting = callback },
      onreconnected: (callback: () => void) => { mock.handlers.reconnected = callback },
      onclose: (callback: () => void) => { mock.handlers.close = callback },
    } }
  },
}))
beforeEach(() => { mock.handlers = {}; mock.start.mockResolvedValue(undefined); mock.stop.mockResolvedValue(undefined); mock.invoke.mockResolvedValue(undefined) })
function setup() {
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidate = vi.spyOn(cache, 'invalidateQueries')
  const remove = vi.spyOn(cache, 'removeQueries')
  const user = new User({ access_token: 'test', token_type: 'Bearer', profile: { sub: 'user', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 } })
  const auth = { user, loading: false, configured: true, error: null, signIn: vi.fn(), signOut: vi.fn(), completeSignIn: vi.fn() }
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={cache}><AuthContext.Provider value={auth}><MemoryRouter initialEntries={['/projects/w/p']}>{children}</MemoryRouter></AuthContext.Provider></QueryClientProvider>
  const rendered = renderHook(() => ({ status: useProjectLive('w', 'p'), path: useLocation().pathname }), { wrapper })
  return { ...rendered, invalidate, remove }
}
it('subscribes, refetches changes, resubscribes after reconnect and stops on unmount', async () => {
  const view = setup()
  await waitFor(() => expect(view.result.current.status).toBe('Live'))
  expect(mock.invoke).toHaveBeenCalledWith('Subscribe', 'w', 'p')
  await waitFor(() => expect(view.invalidate).toHaveBeenCalled())
  view.invalidate.mockClear()
  act(() => mock.handlers.ProjectChanged())
  await waitFor(() => expect(view.invalidate).toHaveBeenCalledWith({ queryKey: ['app-data'] }))
  act(() => mock.handlers.reconnecting())
  expect(view.result.current.status).toBe('Reconnecting')
  act(() => mock.handlers.reconnected())
  await waitFor(() => expect(mock.invoke).toHaveBeenCalledTimes(2))
  await waitFor(() => expect(view.result.current.status).toBe('Live'))
  view.unmount()
  expect(mock.stop).toHaveBeenCalledOnce()
})
it('clears cached project data and leaves the project after access revocation', async () => {
  const view = setup()
  await waitFor(() => expect(view.result.current.status).toBe('Live'))
  act(() => mock.handlers.AccessRevoked())
  expect(view.remove).toHaveBeenCalledWith({ queryKey: ['app-data'] })
  expect(view.result.current.path).toBe('/projects')
})
