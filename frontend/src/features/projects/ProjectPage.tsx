import { useProjectLive } from './useProjectLive'
import { lazy, Suspense, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { analytics, label, list, member, permissions, project, projectPath, projectUrl, statuses, useData, useWrite, type Project } from './data'
import { ErrorMessage, Field, Heading } from './ui'
import { formData, text } from './forms'
import { TaskList } from './TaskList'
import { TaskForm } from './TaskForm'
import { ProjectTeam } from './ProjectTeam'

const KanbanBoard = lazy(() => import('./KanbanBoard').then(module => ({ default: module.KanbanBoard })))

export function ProjectPage() {
  const { workspaceId = '', projectId = '' } = useParams()
  const live = useProjectLive(workspaceId, projectId)
  const path = projectPath(workspaceId, projectId)
  const query = useData(path, project)
  const access = useData(path + '/access', permissions)
  const members = useData(path + '/members', list(member))
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') ?? 'tasks'
  const [creating, setCreating] = useState(false)
  const navigate = useNavigate()
  return <>
    <Link className="text-link mb-5" to={`/projects?workspace=${workspaceId}`}>← All projects</Link>
    <Heading title={query.data?.name ?? 'Project'}>{query.data && <span className="subtle-tag">{query.data.key}{query.data.isArchived ? ' · Archived' : ''}</span>}</Heading>
    <ErrorMessage error={query.error ?? access.error ?? members.error} />
    {query.isLoading && <p role="status">Loading project…</p>}
    {query.data && !query.isError && !access.isError && <>
      <p className="mb-6 whitespace-pre-wrap text-sm text-slate-600">{query.data.description}</p>
      <p className="mb-3 text-xs text-slate-500" role="status">Updates: {live}</p>
      <nav className="project-tabs" aria-label="Project views">
        {['tasks', 'board', 'analytics', 'team', ...(access.data?.canManage ? ['settings'] : [])].map(name =>
          <button key={name} className={tab === name ? 'selected' : ''} aria-current={tab === name ? 'page' : undefined} onClick={() => setParams({ tab: name })}>{name[0].toUpperCase() + name.slice(1)}</button>)}
      </nav>
      {tab === 'tasks' && <>
        {access.data?.canWrite && !query.data.isArchived && <button className="button-primary mb-5" onClick={() => setCreating(!creating)}>{creating ? 'Close task form' : 'New task'}</button>}
        {creating && !query.data.isArchived && access.data?.canWrite && <div className="mb-6"><TaskForm path={path + '/tasks'} members={members.data ?? []} onSaved={t => void navigate(projectUrl(workspaceId, projectId) + '/tasks/' + t.id)} /></div>}
        <TaskList path={path + '/tasks'} members={members.data ?? []} />
      </>}
      {tab === 'board' && <Suspense fallback={<p role="status">Loading board?</p>}><KanbanBoard path={path} canWrite={Boolean(access.data?.canWrite && !query.data.isArchived)} /></Suspense>}
      {tab === 'analytics' && <ProjectAnalytics path={path} />}
      {tab === 'team' && <ProjectTeam path={path} canManage={access.data?.canManage ?? false} archived={query.data.isArchived} />}
      {tab === 'settings' && access.data?.canManage && <ProjectSettings key={query.data.name + query.data.isArchived} path={path} project={query.data} />}
    </>}
  </>
}
function ProjectAnalytics({ path }: { path: string }) {
  const query = useData(path + '/analytics', analytics)
  const stats = query.data
  return <><ErrorMessage error={query.error} />{query.isLoading && <p role="status">Loading analytics…</p>}
    {stats && <>
      <div className="grid gap-4 sm:grid-cols-3">{[['Total tasks', stats.total], ['Completed', stats.completed], ['Overdue', stats.overdue]].map(([name, count]) =>
        <div className="editor-card" key={name}><p className="text-sm text-slate-500">{name}</p><p className="mt-3 text-4xl font-semibold">{count}</p></div>)}</div>
      <div className="mt-6 grid gap-6 lg:grid-cols-2"><section className="editor-card"><h2 className="mb-5">Progress by status</h2>{statuses.map(s => {
        const count = stats.byStatus.find(x => x.status === s)?.count ?? 0
        return <div key={s} className="mb-4"><div className="mb-2 flex justify-between text-sm"><span>{label(s)}</span><span>{count}</span></div><progress className="w-full accent-teal-700" max={Math.max(stats.total, 1)} value={count} aria-label={label(s)} /></div>
      })}</section><section className="editor-card"><h2 className="mb-5">Open work by assignee</h2>{stats.workload.length === 0 && <p className="text-sm text-slate-500">No open tasks.</p>}{stats.workload.map((w, index) => <div className="member-row" key={index}><span>{w.assignee}</span><strong>{w.count}</strong></div>)}</section></div>
    </>}
  </>
}
function ProjectSettings({ path, project: value }: { path: string; project: Project }) {
  const write = useWrite()
  const navigate = useNavigate()
  const [confirming, setConfirming] = useState(false)
  return <><form className="editor-card" onSubmit={event => {
    const form = formData(event)
    write.mutate({ path, method: 'PUT', body: { key: value.key, name: text(form, 'name'), description: text(form, 'description') || null, isArchived: form.get('archived') === 'on' } })
  }}>
    <Field label="Project name"><input name="name" required maxLength={100} defaultValue={value.name} /></Field>
    <Field label="Description"><textarea name="description" maxLength={4000} rows={4} defaultValue={value.description ?? ''} /></Field>
    <label className="my-5 flex items-center gap-2 text-sm"><input type="checkbox" name="archived" defaultChecked={value.isArchived} />Archive project (pause task changes)</label>
    <ErrorMessage error={write.error} /><button className="button-primary" disabled={write.isPending}>Save project</button>
  </form><section className="editor-card mt-6"><h2>Delete project</h2><p className="my-3 text-sm text-slate-600">Remove this project from the workspace. Its records are retained, but it cannot be reopened from this interface.</p>
    {!confirming ? <button className="button-danger" onClick={() => setConfirming(true)}>Delete project</button> : <div className="flex gap-3">
      <button className="button-danger" disabled={write.isPending} onClick={async () => { try { await write.mutateAsync({ path, method: 'DELETE' }); void navigate('/projects?workspace=' + value.workspaceId) } catch { /* shown above */ } }}>Confirm delete</button>
      <button className="button-secondary" onClick={() => setConfirming(false)}>Cancel</button>
    </div>}
  </section></>
}
