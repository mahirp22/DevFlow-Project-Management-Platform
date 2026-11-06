import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { list, project, projectUrl, useData, useWrite, workspace } from './data'
import { ErrorMessage, Field, Heading } from './ui'
import { formData, text } from './forms'

export function ProjectsPage() {
  const workspaces = useData('/workspaces', list(workspace))
  const [params, setParams] = useSearchParams()
  const selected = params.get('workspace') ?? workspaces.data?.[0]?.id ?? ''
  const current = workspaces.data?.find(w => w.id === selected)
  const projects = useData(`/workspaces/${selected}/projects`, list(project), Boolean(selected))
  const createWorkspace = useWrite()
  const createProject = useWrite()
  const [showForm, setShowForm] = useState(false)
  const canManage = current?.role === 'Owner' || current?.role === 'Admin'
  return <>
    <Heading title="Projects"><Link className="button-secondary" to="/invitations">Join with an invitation</Link></Heading>
    <ErrorMessage error={workspaces.error} />
    {workspaces.isLoading && <p role="status">Loading workspaces…</p>}
    <div className="workspace-toolbar">
      <Field label="Workspace"><select value={selected} onChange={e => setParams({ workspace: e.target.value })}>
        {!workspaces.data?.length && <option value="">No workspaces yet</option>}
        {workspaces.data?.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}
      </select></Field>
      {current && <Link className="text-link" to={`/workspaces/${selected}`}>Workspace settings →</Link>}
      <button className="button-secondary" onClick={() => setShowForm(!showForm)}>{showForm ? 'Close workspace form' : 'New workspace'}</button>
    </div>
    {showForm && <form className="editor-card mb-6" onSubmit={async event => {
      const form = formData(event)
      try { const created = workspace(await createWorkspace.mutateAsync({ path: '/workspaces', body: { name: text(form, 'name') } })); setParams({ workspace: created.id }); setShowForm(false) } catch { /* shown below */ }
    }}>
      <h2>Create a workspace</h2><Field label="Workspace name"><input name="name" required maxLength={100} placeholder="Your team or organization" /></Field>
      <ErrorMessage error={createWorkspace.error} /><button className="button-primary" disabled={createWorkspace.isPending}>Create workspace</button>
    </form>}
    {!workspaces.isLoading && !workspaces.data?.length && <div className="empty-state"><h2>A home for your team's work</h2><p>Create your first workspace, or accept an invitation from a teammate. Complete your profile on the Account page first.</p></div>}
    {canManage && <details className="editor-card mb-6"><summary>Create a project</summary><form className="mt-4" onSubmit={async event => {
      const element = event.currentTarget
      const form = formData(event)
      try { await createProject.mutateAsync({ path: `/workspaces/${selected}/projects`, body: { key: text(form, 'key').toUpperCase(), name: text(form, 'name'), description: text(form, 'description') || null } }); element.reset() } catch { /* shown below */ }
    }}>
      <div className="grid gap-4 md:grid-cols-2"><Field label="Project name"><input name="name" required maxLength={100} /></Field><Field label="Project key"><input name="key" required pattern="[A-Za-z][A-Za-z0-9]{1,9}" placeholder="DEV" maxLength={10} /></Field></div>
      <Field label="Description"><textarea name="description" maxLength={4000} rows={3} /></Field><p className="mb-4 text-xs text-slate-500">Keys use 2–10 letters/digits and cannot be changed later.</p>
      <ErrorMessage error={createProject.error} /><button className="button-primary" disabled={createProject.isPending}>Create project</button>
    </form></details>}
    <ErrorMessage error={projects.error} />
    {projects.isLoading && <p role="status">Loading projects…</p>}
    {selected && projects.data?.length === 0 && <div className="empty-state"><h2>No projects yet</h2><p>A workspace owner or admin can create the first project.</p></div>}
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{projects.data?.map(p => <Link className="feature-card" key={p.id} to={projectUrl(p.workspaceId, p.id)}>
      <span className="subtle-tag">{p.key}</span><h2 className="mt-4">{p.name}</h2><p>{p.description || 'Ready for your next idea.'}</p>
      {p.isArchived && <span className="subtle-tag mt-4">Archived</span>}
    </Link>)}</div>
  </>
}
