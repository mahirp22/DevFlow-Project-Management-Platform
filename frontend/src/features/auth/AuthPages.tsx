import { useEffect, useRef, useState } from 'react'
import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, getJson, requestJson } from '../../lib/api'
import { useAuth } from './AuthContext'

export function RequireAuth() {
  const auth = useAuth()
  const location = useLocation()
  if (auth.loading) return <p role="status">Checking your session…</p>
  if (!auth.user || auth.user.expired) return <Navigate to="/sign-in" state={{ returnTo: location.pathname }} replace />
  return <Outlet />
}

export function SignInPage() {
  const auth = useAuth()
  const location = useLocation()
  if (auth.user && !auth.user.expired) return <Navigate to="/account" replace />
  return <section className="empty-state">
    <p className="eyebrow">YOUR TEAM STARTS HERE</p>
    <h1>Welcome to DevFlow.</h1>
    <p>Sign in or create an account to make room for your team's work.</p>
    {auth.error && <p role="alert">{auth.error}</p>}
    {auth.configured ? <>
      <button className="button-primary" disabled={auth.loading} onClick={() => void auth.signIn(location.state?.returnTo)}>Continue to sign in</button>
      <p>New here? Choose Create account on the secure sign-in page. You can also reset your password there.</p>
    </> : <p role="status">Sign-in is not configured yet. Follow the Cognito setup guide in docs/phase-4.md, then restart the frontend.</p>}
    <Link className="text-link" to="/">Back to overview</Link>
  </section>
}

export function CallbackPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const callbackUrl = useRef(window.location.href)
  const [error, setError] = useState(false)
  const complete = useRef(auth.completeSignIn)
  useEffect(() => {
    let active = true
    // Remove authorization codes from the address bar before displaying other content.
    window.history.replaceState(window.history.state, '', '/auth/callback')
    void complete.current(callbackUrl.current).then(path => {
      if (active) void navigate(path, { replace: true })
    }).catch(() => { if (active) setError(true) })
    return () => { active = false }
  }, [navigate])
  return <section className="empty-state">
    <h1>{error ? 'Let’s try that again.' : 'Completing sign-in…'}</h1>
    {error ? <><p role="alert">The sign-in request could not be verified. Please start a new sign-in.</p><Link className="button-primary" to="/sign-in">Back to sign in</Link></> : <p role="status">Securely connecting your account.</p>}
  </section>
}

type Profile = { id: string; displayName: string; email: string }
function parseProfile(body: unknown): Profile {
  if (!body || typeof body !== 'object' || !('id' in body) || typeof body.id !== 'string'
    || !('displayName' in body) || typeof body.displayName !== 'string'
    || !('email' in body) || typeof body.email !== 'string') throw new Error('Invalid profile response.')
  return { id: body.id, displayName: body.displayName, email: body.email }
}

export function AccountPage() {
  const auth = useAuth()
  const cache = useQueryClient()
  const token = auth.user?.access_token
  const query = useQuery({
    queryKey: ['profile', auth.user?.profile.sub],
    queryFn: async ({ signal }) => parseProfile(await getJson('/me', signal, token)),
    enabled: Boolean(token),
  })
  const initialize = useMutation({
    mutationFn: async () => parseProfile(await requestJson('/me', { method: 'POST', token })),
    onSuccess: () => { void query.refetch() },
  })
  const rename = useMutation({
    mutationFn: async (displayName: string) => parseProfile(await requestJson('/me', { method: 'PUT', token, body: { displayName } })),
    onSuccess: async profile => {
      cache.setQueryData(['profile', auth.user?.profile.sub], profile)
      await cache.invalidateQueries({ queryKey: ['app-data'] })
    },
  })
  const missing = query.error instanceof ApiError && query.error.status === 404
  const unauthorized = query.error instanceof ApiError && query.error.status === 401
  return <section className="connection-card">
    <p className="eyebrow">YOUR ACCOUNT</p><h1>Make yourself at home.</h1>
    {query.isPending && <p role="status">Loading your profile…</p>}
    {query.data && <div className="my-6"><h2>{query.data.displayName}</h2><p>{query.data.email}</p></div>}
    {query.data && <form className="my-6" onSubmit={event => {
      event.preventDefault()
      const displayName = String(new FormData(event.currentTarget).get('displayName') ?? '').trim()
      rename.mutate(displayName)
    }}>
      <label className="field"><span>Display name</span><input key={query.data.id} name="displayName" required maxLength={100} defaultValue={query.data.displayName === 'DevFlow member' ? '' : query.data.displayName} placeholder="Your name" onChange={() => rename.reset()} /></label>
      <p className="mb-4 text-sm text-slate-600">This name appears in your teams, task assignments and comments.</p>
      <button className="button-primary" disabled={rename.isPending}>{rename.isPending ? 'Saving?' : 'Save name'}</button>
      {rename.isSuccess && <p role="status" className="mt-3">Your display name is saved.</p>}
      {rename.isError && <p role="alert" className="mt-3">{rename.error.message}</p>}
    </form>}
    {missing && <><p>Finish setting up your DevFlow profile using your verified account details.</p><button className="button-primary mt-5" disabled={initialize.isPending} onClick={() => initialize.mutate()}>{initialize.isPending ? 'Setting up…' : 'Set up my profile'}</button></>}
    {query.isError && !missing && <p role="alert">{unauthorized ? 'Your session is no longer accepted. Sign out and sign in again.' : 'Your profile could not be loaded. Check the API and database connection.'}</p>}
    {initialize.isError && <p role="alert">Profile setup failed. Verify your email and check the connection, then try again.</p>}
    <div className="mt-6 flex flex-wrap gap-3">
      {query.isError && !missing && <button className="button-secondary" onClick={() => void query.refetch()}>Retry</button>}
      <button className="button-secondary" onClick={() => void auth.signOut()}>Sign out</button>
    </div>
  </section>
}
