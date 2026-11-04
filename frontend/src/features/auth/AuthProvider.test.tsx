import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { User } from 'oidc-client-ts'
import { createQueryClient } from '../../lib/queryClient'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './AuthContext'

const manager = vi.hoisted(() => ({
  getUser: vi.fn(), clearStaleState: vi.fn().mockResolvedValue(undefined),
  removeUser: vi.fn().mockResolvedValue(undefined), signinRedirect: vi.fn().mockResolvedValue(undefined),
  signinRedirectCallback: vi.fn(), revokeTokens: vi.fn().mockResolvedValue(undefined),
  events: { addAccessTokenExpired: vi.fn(), removeAccessTokenExpired: vi.fn() },
}))
vi.mock('./oidc', () => ({
  authConfiguration: { domain: 'https://login.example.invalid', clientId: 'test-client' },
  authConfigurationError: null, userManager: manager,
  safeReturnPath: (value: unknown) => value === '/account' ? '/account' : '/',
  cognitoLogoutUrl: () => 'https://login.example.invalid/logout',
}))

function Consumer() {
  const auth = useAuth()
  return <><p>{auth.loading ? 'Loading' : auth.user ? 'Signed in' : 'Signed out'}</p>
    {auth.error && <p role="alert">{auth.error}</p>}
    <button onClick={() => void auth.signIn('/account')}>Sign in</button>
    <button onClick={() => void auth.completeSignIn('https://app.example.invalid/auth/callback?code=test').catch(() => {})}>Complete</button>
  </>
}

function setup() {
  const client = createQueryClient()
  render(<QueryClientProvider client={client}><AuthProvider><Consumer /></AuthProvider></QueryClientProvider>)
  return client
}

describe('session lifecycle', () => {
  beforeEach(() => { manager.getUser.mockResolvedValue(null) })

  it('clears cached private data and signs out when the token expires', async () => {
    manager.getUser.mockResolvedValue(new User({ access_token: 'test-only', token_type: 'Bearer', profile: { sub: 'user', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 } }))
    const client = setup()
    expect(await screen.findByText('Signed in')).toBeInTheDocument()
    client.setQueryData(['private'], { email: 'member@example.invalid' })
    act(() => { manager.events.addAccessTokenExpired.mock.calls[0][0]() })
    expect(screen.getByText('Signed out')).toBeInTheDocument()
    expect(client.getQueryData(['private'])).toBeUndefined()
    expect(manager.removeUser).toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('session expired')
  })

  it('starts the provider redirect with an application return route', async () => {
    setup()
    await screen.findByText('Signed out')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(manager.signinRedirect).toHaveBeenCalledWith({ state: { returnTo: '/account' } })
  })

  it('exchanges a callback once and clears previous account data', async () => {
    manager.signinRedirectCallback.mockResolvedValue(new User({ access_token: 'new-test-token', token_type: 'Bearer', profile: { sub: 'new-user', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 }, userState: { returnTo: '/account' } }))
    const client = setup()
    await screen.findByText('Signed out')
    client.setQueryData(['private'], 'previous user data')
    await userEvent.click(screen.getByRole('button', { name: 'Complete' }))
    await userEvent.click(screen.getByRole('button', { name: 'Complete' }))
    await waitFor(() => expect(screen.getByText('Signed in')).toBeInTheDocument())
    expect(manager.signinRedirectCallback).toHaveBeenCalledOnce()
    expect(client.getQueryData(['private'])).toBeUndefined()
  })
})
