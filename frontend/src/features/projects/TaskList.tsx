import { useState } from 'react'
import { Link } from 'react-router'
import { label, page, priorities, projectUrl, statuses, task, useData, type Member } from './data'
import { ErrorMessage, Field, Pager } from './ui'
import { formData, text } from './forms'

export function TaskList({ path, members = [] }: { path: string; members?: Member[] }) {
  const [filters, setFilters] = useState('')
  const [currentPage, setPage] = useState(1)
  const query = useData(`${path}?page=${currentPage}&${filters}`, page(task))
  return <>
    <form className="filter-bar" onSubmit={event => {
      const form = formData(event)
      const params = new URLSearchParams()
      for (const key of ['search', 'status', 'priority', 'assigneeUserId']) if (text(form, key)) params.set(key, text(form, key))
      if (form.get('overdue')) params.set('overdue', 'true')
      setFilters(params.toString()); setPage(1)
    }}>
      <Field label="Search tasks"><input name="search" maxLength={200} placeholder="Search by title…" /></Field>
      <Field label="Status"><select name="status"><option value="">All statuses</option>{statuses.map(s => <option key={s} value={s}>{label(s)}</option>)}</select></Field>
      <Field label="Priority"><select name="priority"><option value="">All priorities</option>{priorities.map(s => <option key={s}>{s}</option>)}</select></Field>
      {members.length > 0 && <Field label="Assignee"><select name="assigneeUserId"><option value="">Everyone</option>{members.map(m => <option key={m.userId} value={m.userId}>{m.displayName}</option>)}</select></Field>}
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" name="overdue" />Overdue</label>
      <button className="button-secondary">Apply filters</button>
    </form>
    <ErrorMessage error={query.error} />
    {query.isLoading && <p role="status">Loading tasks…</p>}
    {query.data?.items.length === 0 && <div className="empty-state min-h-60"><h2>No tasks to show</h2><p>Create a task or adjust your filters.</p></div>}
    <div className="task-list">{query.data?.items.map(t => <Link className="task-row" key={t.id} to={projectUrl(t.workspaceId, t.projectId) + '/tasks/' + t.id}>
      <div className="min-w-0 flex-1"><span className="text-xs text-slate-500">{t.projectKey}-{t.number}</span><h3 className="break-words">{t.title}</h3><p className="mt-1 text-xs text-slate-500">{t.assigneeName ?? 'Unassigned'}{t.dueDate ? ` · Due ${t.dueDate}` : ''}</p></div>
      <span className={`status-badge status-${t.status}`}>{label(t.status)}</span><span className="subtle-tag">{t.priority}</span>
    </Link>)}</div>
    {query.data && <Pager page={currentPage} total={query.data.total} pageSize={query.data.pageSize} onChange={setPage} />}
  </>
}
