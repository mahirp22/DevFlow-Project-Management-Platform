import { useState } from 'react'

type ApiStatus = 'idle' | 'checking' | 'healthy' | 'unavailable'

function App() {
  const [status, setStatus] = useState<ApiStatus>('idle')

  async function checkApi() {
    setStatus('checking')
    try {
      const response = await fetch('/api/health', {
        signal: AbortSignal.timeout(5000),
        cache: 'no-store',
      })
      const body: unknown = await response.json()
      const healthy = response.ok && typeof body === 'object' && body !== null
        && 'status' in body && body.status === 'Healthy'
      setStatus(healthy ? 'healthy' : 'unavailable')
    } catch {
      setStatus('unavailable')
    }
  }

  const messages: Record<ApiStatus, string> = {
    idle: 'Start the API, then check the connection.',
    checking: 'Connecting to the API…',
    healthy: 'API is running. The development connection is ready.',
    unavailable: 'API unavailable. Confirm the backend is running on port 5080.',
  }

  return (
    <main>
      <div className="brand"><span aria-hidden="true">D</span> DevFlow</div>
      <section aria-labelledby="title">
        <p className="eyebrow">PHASE 01 · FOUNDATION</p>
        <h1 id="title">A clear start.<br />Room to build.</h1>
        <p className="intro">The foundation for a collaborative project management platform.
          Built incrementally, with thoughtful engineering at every step.</p>
        <div className="connection">
          <h2>Development connection</h2>
          <p role="status" aria-live="polite">{messages[status]}</p>
          <button onClick={() => void checkApi()} disabled={status === 'checking'}>
            {status === 'checking' ? 'Checking…' : 'Check API connection'}
          </button>
        </div>
        <p className="note">Project features arrive in later phases. This page verifies the application skeleton.</p>
      </section>
      <footer>React + TypeScript <span aria-hidden="true">/</span> ASP.NET Core</footer>
    </main>
  )
}

export default App
