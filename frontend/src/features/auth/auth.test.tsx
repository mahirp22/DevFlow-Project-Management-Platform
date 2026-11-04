import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router'
import { QueryClientProvider } from '@tanstack/react-query'
import { User } from 'oidc-client-ts'
import App from '../../App'
import { AuthContext, type AuthState } from './AuthContext'
import { createQueryClient } from '../../lib/queryClient'
import { cognitoLogoutUrl, readAuthConfig, safeReturnPath } from './oidc'

function renderAuth(path: string, overrides: Partial<AuthState> = {}) {
  const auth: AuthState = {
    configured: false, loading: false, user: null, error: null,
    signIn: vi.fn().mockResolvedValue(undefined), signOut: vi.fn().mockResolvedValue(undefined),
    completeSignIn: vi.fn().mockRejectedValue(new Error('Invalid callback')), ...overrides,
  }
  render(<QueryClientProvider client={createQueryClient()}><AuthContext.Provider value={auth}>
    <MemoryRouter initialEntries={[path]}><App /></MemoryRouter>
  </AuthContext.Provider></QueryClientProvider>)
  return auth
}

describe('authentication boundaries', () => {
  it('redirects anonymous project access to a clear configuration message', async () => {
    renderAuth('/projects')
    expect(await screen.findByRole('heading', { name: 'Welcome to DevFlow.' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Sign-in is not configured')
  })

  it('preserves an allowed destination when starting sign-in', async () => {
    const auth = renderAuth('/tasks', { configured: true })
    await userEvent.click(await screen.findByRole('button', { name: 'Continue to sign in' }))
    expect(auth.signIn).toHaveBeenCalledWith('/tasks')
  })

  it('rejects external return destinations', () => {
    for (const path of ['https://evil.example', '//evil.example', '/\\evil.example', '/auth/callback', undefined])
      expect(safeReturnPath(path)).toBe('/')
    expect(safeReturnPath('/account')).toBe('/account')
  })

  it('shows a recoverable callback error without revealing provider details', async () => {
    renderAuth('/auth/callback')
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be verified')
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute('href', '/sign-in')
  })

  it('creates a profile only after explicit action and sends the access token', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(Response.json({ title: 'Not found' }, { status: 404 }))
      .mockImplementation(() => Promise.resolve(Response.json({ id: '123', displayName: 'Member', email: 'member@example.invalid' })))
    vi.stubGlobal('fetch', fetchMock)
    const user = new User({ access_token: 'test-only-access', token_type: 'Bearer', profile: { sub: '123', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 } })
    const auth = renderAuth('/account', { configured: true, user })
    await userEvent.click(await screen.findByRole('button', { name: 'Set up my profile' }))
    expect(await screen.findByText('member@example.invalid')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/me', expect.objectContaining({
      method: 'POST', headers: expect.objectContaining({ Authorization: 'Bearer test-only-access' }),
    }))
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(auth.signOut).toHaveBeenCalledOnce()
  })
})

describe('Cognito public configuration', () => {
  it('permits an unconfigured shell but rejects partial or insecure configuration', () => {
    expect(readAuthConfig({})).toBeNull()
    expect(() => readAuthConfig({ VITE_COGNITO_CLIENT_ID: 'public-client' })).toThrow()
    expect(() => readAuthConfig({ VITE_COGNITO_CLIENT_ID: 'public-client', VITE_COGNITO_AUTHORITY: 'http://issuer.invalid/pool', VITE_COGNITO_DOMAIN: 'https://login.invalid' })).toThrow()
  })
  it('uses Cognito logout_uri and client_id parameters', () => {
    const url = new URL(cognitoLogoutUrl('https://login.example.invalid', 'public-client', 'http://localhost:5173'))
    expect(url.pathname).toBe('/logout')
    expect(url.searchParams.get('logout_uri')).toBe('http://localhost:5173/sign-in')
    expect(url.searchParams.get('client_id')).toBe('public-client')
  })
})
