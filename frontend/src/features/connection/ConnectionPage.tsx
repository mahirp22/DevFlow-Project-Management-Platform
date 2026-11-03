import { useQuery } from '@tanstack/react-query'
import { ApiError, getJson } from '../../lib/api'

async function getHealth(path: string, signal?: AbortSignal) {
  const body = await getJson(path, signal)
  if (typeof body !== 'object' || body === null || !('status' in body) || body.status !== 'Healthy') {
    throw new Error('The service did not report a healthy status.')
  }
  return body.status
}

function ConnectionCheck({ title, description, path }: { title: string; description: string; path: string }) {
  const query = useQuery({ queryKey: ['health', path], queryFn: ({ signal }) => getHealth(path, signal), enabled: false })
  return <section className="connection-card">
    <div className="flex items-start justify-between gap-4"><div><h2>{title}</h2><p>{description}</p></div><span className={`connection-indicator ${query.isSuccess && !query.isFetching ? 'healthy' : ''}`} aria-hidden="true" /></div>
    <div role="status" className="connection-result">{query.isFetching ? 'Checking connection…' : query.isError ? 'Unable to connect. Confirm the backend and database are running, then try again.' : query.isSuccess ? 'Connected and healthy.' : 'Ready to check.'}</div>
    {query.isError && query.error instanceof ApiError && query.error.traceId && <p className="text-sm break-all">Reference: {query.error.traceId}</p>}
    <button className="button-secondary" disabled={query.isFetching} onClick={() => void query.refetch()}>{query.isFetching ? 'Checking…' : query.isError ? `Retry ${title.toLowerCase()}` : `Check ${title.toLowerCase()}`}</button>
  </section>
}
export function ConnectionPage() {
  return <><div className="page-heading"><div><p className="eyebrow">STAY CONNECTED</p><h1>A healthy foundation.</h1><p>Check that DevFlow’s services are ready for your work.</p></div></div>
    <div className="grid gap-5 lg:grid-cols-2"><ConnectionCheck title="API" description="Can the application reach the server?" path="/health" /><ConnectionCheck title="Database" description="Is the database connected and up to date?" path="/health/ready" /></div>
    <p className="mt-6 text-sm text-slate-500">These checks run when you request them. A successful API check does not guarantee database readiness.</p></>
}