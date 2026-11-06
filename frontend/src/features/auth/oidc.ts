import { InMemoryWebStorage, UserManager, WebStorageStateStore } from 'oidc-client-ts'

export function readAuthConfig(env: Record<string, string | undefined>) {
  const authority = env.VITE_COGNITO_AUTHORITY?.trim()
  const clientId = env.VITE_COGNITO_CLIENT_ID?.trim()
  const domain = env.VITE_COGNITO_DOMAIN?.trim()
  if (!authority && !clientId && !domain) return null
  if (!authority || !clientId || !domain) throw new Error('Complete all three Cognito configuration values.')
  const issuer = new URL(authority)
  const host = new URL(domain)
  if (issuer.protocol !== 'https:' || host.protocol !== 'https:' || host.pathname !== '/'
    || issuer.username || issuer.password || host.username || host.password
    || issuer.search || issuer.hash || host.search || host.hash) {
    throw new Error('Cognito configuration requires HTTPS issuer and domain URLs.')
  }
  return { authority: authority.replace(/\/$/, ''), clientId, domain: host.origin }
}

let configuration: ReturnType<typeof readAuthConfig> = null
let configurationError: string | null = null
try { configuration = readAuthConfig(import.meta.env) }
catch { configurationError = 'Sign-in configuration is incomplete or invalid. Follow docs/phase-4.md.' }

export const authConfiguration = configuration
export const authConfigurationError = configurationError
export const userManager = configuration ? new UserManager({
  authority: configuration.authority,
  client_id: configuration.clientId,
  redirect_uri: window.location.origin + '/auth/callback',
  response_type: 'code',
  scope: 'openid email profile',
  // PKCE and state validation are handled by oidc-client-ts.
  userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
  stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: false,
  loadUserInfo: true,
  monitorSession: false,
  requestTimeoutInSeconds: 10,
}) : null

export function safeReturnPath(value: unknown): string {
  // Only application routes are accepted, never arbitrary redirect URLs.
  if (typeof value !== 'string') return '/'
  if (['/projects', '/tasks', '/account', '/invitations', '/'].includes(value)) return value
  const id = '[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}'
  return new RegExp('^(/projects/' + id + '/' + id + '(/tasks/' + id + ')?|/workspaces/' + id + ')$').test(value) ? value : '/'
}

export function cognitoLogoutUrl(domain: string, clientId: string, origin: string) {
  const url = new URL('/logout', domain)
  url.searchParams.set('client_id', clientId)
  url.searchParams.set('logout_uri', origin + '/sign-in')
  return url.toString()
}
