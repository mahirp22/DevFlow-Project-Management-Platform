import { Link, useParams } from 'react-router'
import { list, member, useData, useWrite, workspace } from './data'
import { ErrorMessage, Field, Heading } from './ui'
import { formData, text } from './forms'
export function WorkspacePage() {
  const { workspaceId = '' } = useParams()
  const workspaces = useData('/workspaces', list(workspace))
  const current = workspaces.data?.find(w => w.id === workspaceId)
  const members = useData(`/workspaces/${workspaceId}/members`, list(member))
  const write = useWrite()
  const admin = current?.role === 'Owner' || current?.role === 'Admin'
  return <>
    <Heading title={current?.name ?? 'Workspace settings'}><Link className="text-link" to={`/projects?workspace=${workspaceId}`}>Back to projects</Link></Heading>
    <ErrorMessage error={workspaces.error ?? members.error ?? write.error} />
    {admin && <form className="editor-card mb-6" key={current?.name} onSubmit={event => { const form = formData(event); write.mutate({ path: `/workspaces/${workspaceId}`, method: 'PUT', body: { name: text(form, 'name') } }) }}>
      <Field label="Workspace name"><input name="name" required maxLength={100} defaultValue={current?.name} /></Field>
      <button className="button-primary" disabled={write.isPending}>Save name</button>
    </form>}
    <h2 className="mb-4">Members</h2>
    {members.isLoading && <p role="status">Loading members…</p>}
    {members.data?.map(m => <div className="member-row" key={m.userId}><div><strong>{m.displayName}</strong><p>{m.email}</p></div>
      {current?.role === 'Owner' ? <Field label={`Role for ${m.displayName}`}><select value={m.role} disabled={write.isPending} onChange={e => write.mutate({ path: `/workspaces/${workspaceId}/members/${m.userId}`, method: 'PUT', body: { role: e.target.value } })}>
        {['Owner', 'Admin', 'Member'].map(role => <option key={role}>{role}</option>)}
      </select></Field> : <span className="subtle-tag">{m.role}</span>}
    </div>)}
    <p className="mt-4 text-sm text-slate-500">Invite teammates from a project's Team tab. Only owners can change workspace roles; a workspace always retains an owner.</p>
  </>
}
