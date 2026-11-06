import { useEffect, useState } from 'react'
import { invitation, invitationCreated, list, member, useData, useWrite } from './data'
import { ErrorMessage, Field } from './ui'
import { formData, text } from './forms'

export function ProjectTeam({ path, canManage, archived }: { path: string; canManage: boolean; archived: boolean }) {
  const members = useData(path + '/members', list(member))
  const invites = useData(path + '/invitations', list(invitation), canManage)
  const write = useWrite()
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => { const timer = window.setInterval(() => setNow(Date.now()), 60_000); return () => window.clearInterval(timer) }, [])
  const [code, setCode] = useState('')
  const [copied, setCopied] = useState(false)
  const [removing, setRemoving] = useState<string | null>(null)
  return <>
    <ErrorMessage error={members.error ?? invites.error ?? write.error} />
    <h2 className="mb-4">Project members</h2>
    {members.data?.map(m => <div key={m.userId} className="member-row"><div><strong>{m.displayName}</strong><p>{m.email}</p></div>
      {canManage ? <div className="flex flex-wrap items-end gap-3">
        <Field label={`Role for ${m.displayName}`}><select value={m.role} disabled={write.isPending} onChange={e => write.mutate({ path: path + '/members/' + m.userId, method: 'PUT', body: { role: e.target.value } })}>{['Manager', 'Member', 'Viewer'].map(r => <option key={r}>{r}</option>)}</select></Field>
        {removing !== m.userId ? <button className="button-secondary" onClick={() => setRemoving(m.userId)}>Remove</button>
          : <><button className="button-danger" disabled={write.isPending} onClick={() => write.mutate({ path: path + '/members/' + m.userId, method: 'DELETE' }, { onSuccess: () => setRemoving(null) })}>Confirm removal</button><button className="text-link" onClick={() => setRemoving(null)}>Cancel</button></>}
      </div> : <span className="subtle-tag">{m.role}</span>}
    </div>)}
    {canManage && !archived && <form className="editor-card mt-6" onSubmit={async event => {
      const form = formData(event)
      try { const result = invitationCreated(await write.mutateAsync({ path: path + '/invitations', body: { email: text(form, 'email'), role: text(form, 'role') } })); setCode(result.token); setCopied(false) } catch { /* shown above */ }
    }}>
      <h2>Invite a teammate</h2><p className="my-3 text-sm text-slate-600">Create a code and send it to your teammate. They must accept using this verified email address. Codes expire in seven days.</p>
      <div className="grid gap-4 sm:grid-cols-2"><Field label="Email address"><input name="email" type="email" required maxLength={320} /></Field><Field label="Project role"><select name="role" defaultValue="Member">{['Manager', 'Member', 'Viewer'].map(r => <option key={r}>{r}</option>)}</select></Field></div>
      <button className="button-primary mt-4" disabled={write.isPending}>Create invitation</button>
      {code && <div className="mt-5 rounded-lg bg-teal-50 p-4"><p className="mb-2 text-sm">Copy this code now. It will not be shown again after leaving this view.</p><code className="block break-all text-xs">{code}</code><button className="button-secondary mt-3" type="button" onClick={async () => { try { await navigator.clipboard.writeText(code); setCopied(true) } catch { setCopied(false) } }}>{copied ? 'Copied' : 'Copy code'}</button><p className="mt-2 text-xs">Your teammate can paste it under Projects → Join with an invitation.</p></div>}
    </form>}
    {canManage && <section className="mt-7"><h2 className="mb-3">Recent invitations</h2>{invites.data?.length === 0 && <p className="text-sm text-slate-500">No invitations yet.</p>}{invites.data?.map(i => <div className="member-row" key={i.id}><div><strong>{i.email}</strong><p>{i.role} · {i.acceptedAt ? 'Accepted' : i.isRevoked ? 'Revoked' : new Date(i.expiresAt).getTime() <= now ? 'Expired' : 'Pending'} · Expires {new Date(i.expiresAt).toLocaleDateString()}</p></div>
      {!i.acceptedAt && !i.isRevoked && <button className="button-secondary" disabled={write.isPending} onClick={() => write.mutate({ path: path + '/invitations/' + i.id, method: 'DELETE' })}>Revoke</button>}
    </div>)}</section>}
  </>
}
