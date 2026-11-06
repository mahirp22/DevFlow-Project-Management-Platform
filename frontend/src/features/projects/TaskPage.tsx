import { useProjectLive } from './useProjectLive'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { activity, comment, label, list, member, page, permissions, project, projectPath, projectUrl, task, useData, useWrite } from './data'
import { TaskForm } from './TaskForm'
import { ErrorMessage, Field, Heading, Pager } from './ui'
import { formData, text } from './forms'

export function TaskPage() {
  const { workspaceId = '', projectId = '', taskId = '' } = useParams()
  const live = useProjectLive(workspaceId, projectId)
  const [reload, setReload] = useState(0)
  const base = projectPath(workspaceId, projectId)
  const path = base + '/tasks/' + taskId
  const query = useData(path, task)
  const access = useData(base + '/access', permissions)
  const projectQuery = useData(base, project)
  const members = useData(base + '/members', list(member))
  const [commentPage, setCommentPage] = useState(1)
  const [activityPage, setActivityPage] = useState(1)
  const comments = useData(path + '/comments?page=' + commentPage, page(comment))
  const activities = useData(path + '/activity?page=' + activityPage, page(activity))
  const write = useWrite()
  const navigate = useNavigate()
  const [confirming, setConfirming] = useState(false)
  const canWrite = access.data?.canWrite && projectQuery.data && !projectQuery.data.isArchived
  return <>
    <Link className="text-link mb-5" to={projectUrl(workspaceId, projectId)}>← Back to project</Link>
    <Heading title={query.data ? query.data.projectKey + '-' + query.data.number : 'Task'}>
      <button className="button-secondary" onClick={async () => { const result = await query.refetch(); if (result.isSuccess) setReload(value => value + 1) }}>Reload task</button>
    </Heading>
    <ErrorMessage error={query.error ?? access.error ?? projectQuery.error ?? members.error ?? write.error} />
    {query.isLoading && <p role="status">Loading task…</p>}
    <p className="mb-3 text-xs text-slate-500" role="status">Updates: {live}</p>
    {query.data && !query.isError && !access.isError && <>
      {canWrite ? <TaskForm key={taskId + ':' + reload} path={path} initial={query.data} members={members.data ?? []} /> :
        <section className="editor-card"><h2>{query.data.title}</h2><p className="my-4 whitespace-pre-wrap">{query.data.description || 'No description yet.'}</p><p className="text-sm text-slate-500">{label(query.data.status)} · {query.data.priority} · {query.data.assigneeName ?? 'Unassigned'}{query.data.dueDate ? ' · Due ' + query.data.dueDate : ''}</p></section>}
      <div className="mt-7 grid gap-6 lg:grid-cols-2">
        <section className="editor-card"><h2>Comments</h2><ErrorMessage error={comments.error} />
          {canWrite && <form className="my-5" onSubmit={async event => { const element = event.currentTarget; const form = formData(event); try { await write.mutateAsync({ path: path + '/comments', body: { body: text(form, 'body') } }); element.reset(); setCommentPage(1) } catch { /* shown above */ } }}>
            <Field label="Add a comment"><textarea name="body" required maxLength={4000} rows={3} /></Field><button className="button-secondary" disabled={write.isPending}>Post comment</button>
          </form>}
          {comments.data?.items.length === 0 && <p className="my-4 text-sm text-slate-500">Start the conversation.</p>}
          {comments.data?.items.map(c => <article className="discussion-item" key={c.id}><strong>{c.author}</strong><time>{new Date(c.createdAt).toLocaleString()}</time><p>{c.body}</p></article>)}
          {comments.data && <Pager page={commentPage} total={comments.data.total} pageSize={25} onChange={setCommentPage} />}
        </section>
        <section className="editor-card"><h2>Activity</h2><ErrorMessage error={activities.error} />{activities.data?.items.map(a => <article className="discussion-item" key={a.id}><strong>{a.actor}</strong><time>{new Date(a.createdAt).toLocaleString()}</time><p>{a.summary}</p></article>)}
          {activities.data && <Pager page={activityPage} total={activities.data.total} pageSize={25} onChange={setActivityPage} />}
        </section>
      </div>
      {canWrite && <div className="mt-6">{!confirming ? <button className="button-danger" onClick={() => setConfirming(true)}>Delete task</button> :
        <div className="flex gap-3"><button className="button-danger" disabled={write.isPending} onClick={async () => {
          try { await write.mutateAsync({ path: path + '?version=' + query.data!.version, method: 'DELETE' }); void navigate(projectUrl(workspaceId, projectId)) } catch { /* shown above */ }
        }}>Confirm delete</button><button className="button-secondary" onClick={() => setConfirming(false)}>Cancel</button></div>}</div>}
    </>}
  </>
}
