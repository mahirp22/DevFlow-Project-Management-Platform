import { useEffect, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type { User } from 'oidc-client-ts'
import { AuthContext } from './AuthContext'
import { authConfiguration, authConfigurationError, cognitoLogoutUrl, safeReturnPath, userManager } from './oidc'

// React StrictMode can mount callback effects twice. Exchange each callback code once.
let callback: { url: string; promise: Promise<User> } | null = null

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(Boolean(userManager))
  const [error, setError] = useState<string | null>(authConfigurationError)
  const cache = useQueryClient()

  useEffect(() => {
    const manager = userManager
    let active = true
    void manager?.clearStaleState().catch(() => {})
    if (!manager) return
    void manager.getUser().then(current => {
      if (active) { setUser(current && !current.expired ? current : null); setLoading(false) }
    }).catch(() => { if (active) { setLoading(false); setError('Unable to restore sign-in. Please try again.') } })
    const expired = () => {
      setUser(null)
      cache.clear()
      setError('Your session expired. Please sign in again.')
      void manager.removeUser()
    }
    manager.events.addAccessTokenExpired(expired)
    return () => { active = false; manager.events.removeAccessTokenExpired(expired) }
  }, [cache])

  async function signIn(returnTo = '/account') {
    if (!userManager) return
    setError(null)
    try { await userManager.signinRedirect({ state: { returnTo: safeReturnPath(returnTo) } }) }
    catch { setError('Unable to open sign-in. Check your connection and try again.') }
  }

  async function completeSignIn(url: string) {
    if (!userManager) throw new Error('Sign-in is not configured.')
    if (!callback || callback.url !== url) callback = { url, promise: userManager.signinRedirectCallback(url) }
    try {
      const current = await callback.promise
      cache.clear()
      setUser(current)
      setLoading(false)
      setError(null)
      const state = current.state as { returnTo?: unknown } | undefined
      return safeReturnPath(state?.returnTo)
    } catch {
      setUser(null)
      cache.clear()
      await userManager.removeUser()
      setError('Sign-in could not be completed. Please start again.')
      throw new Error('Sign-in could not be completed. Please start again.')
    }
  }

  async function signOut() {
    setUser(null)
    cache.clear()
    if (!userManager || !authConfiguration) return
    // Local credentials are removed even if the provider's revocation endpoint is unreachable.
    try { await userManager.revokeTokens(['refresh_token']) } catch { /* Expiring access tokens may remain valid until expiry. */ }
    finally {
      await userManager.removeUser()
      window.location.assign(cognitoLogoutUrl(authConfiguration.domain, authConfiguration.clientId, window.location.origin))
    }
  }

  return <AuthContext.Provider value={{
    configured: Boolean(userManager), loading, user, error, signIn, signOut, completeSignIn,
  }}>{children}</AuthContext.Provider>
}
