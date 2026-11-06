import { useState } from 'react'
import { label, priorities, statuses, task, useWrite, type Member, type Task } from './data'
import { ErrorMessage, Field } from './ui'
import { formData, text } from './forms'

export function TaskForm({ path, initial: latest, members, onSaved }: { path: string; initial?: Task; members: Member[]; onSaved?: (result: Task) => void }) {
  const [initial, setInitial] = useState(latest)
  const write = useWrite()
  const [saved, setSaved] = useState(false)
  return <form className="editor-card" onChange={() => setSaved(false)} onSubmit={async event => {
    const form = formData(event)
    try {
      const result = task(await write.mutateAsync({ path, method: initial ? 'PUT' : 'POST', body: {
        title: text(form, 'title'), description: text(form, 'description') || null,
        status: text(form, 'status'), priority: text(form, 'priority'),
        dueDate: text(form, 'dueDate') || null, assigneeUserId: text(form, 'assigneeUserId') || null,
        ...(initial ? { version: initial.version } : {}),
      } }))
      setInitial(result); setSaved(true); onSaved?.(result)
    } catch { /* Keep the draft on failure, including optimistic concurrency conflicts. */ }
  }}>
    {initial && latest && initial.version !== latest.version && <p role="status" className="mb-4 text-sm text-amber-800">This task has newer changes. Your draft is preserved; copy your edits before choosing Reload task.</p>}
    <Field label="Title"><input name="title" required maxLength={200} defaultValue={initial?.title} /></Field>
    <Field label="Description"><textarea name="description" rows={5} maxLength={10000} defaultValue={initial?.description ?? ''} /></Field>
    <div className="grid gap-4 sm:grid-cols-2">
      <Field label="Status"><select name="status" defaultValue={initial?.status ?? 'ToDo'}>{statuses.map(s => <option value={s} key={s}>{label(s)}</option>)}</select></Field>
      <Field label="Priority"><select name="priority" defaultValue={initial?.priority ?? 'Medium'}>{priorities.map(s => <option key={s}>{s}</option>)}</select></Field>
      <Field label="Due date"><input name="dueDate" type="date" defaultValue={initial?.dueDate ?? ''} /></Field>
      <Field label="Assignee"><select name="assigneeUserId" defaultValue={initial?.assigneeUserId ?? ''}><option value="">Unassigned</option>{members.map(m => <option key={m.userId} value={m.userId}>{m.displayName}</option>)}</select></Field>
    </div>
    <ErrorMessage error={write.error} />
    {write.error && <p className="mb-3 text-sm">Your draft is still here. If the task changed, copy any edits you want to keep before reloading it.</p>}
    {saved && <p role="status" className="my-3 text-sm text-teal-700">Task saved.</p>}
    <button className="button-primary mt-4" disabled={write.isPending}>{write.isPending ? 'Saving…' : initial ? 'Save task' : 'Create task'}</button>
  </form>
}
