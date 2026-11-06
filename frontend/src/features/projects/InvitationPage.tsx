import { useNavigate } from 'react-router'
import { project, projectUrl, useWrite } from './data'
import { ErrorMessage, Field, Heading } from './ui'
import { formData, text } from './forms'
export function InvitationPage() {
  const write = useWrite()
  const navigate = useNavigate()
  return <><Heading title="Join your team" /><form className="editor-card max-w-xl" onSubmit={async event => {
    const form = formData(event)
    try { const result = project(await write.mutateAsync({ path: '/invitations/accept', body: { token: text(form, 'token') } })); void navigate(projectUrl(result.workspaceId, result.id)) } catch { /* shown below */ }
  }}>
    <p className="mb-5 text-sm text-slate-600">Paste the invitation code your teammate shared. Sign in with the invited email and set up your Account profile first.</p>
    <Field label="Invitation code"><input name="token" required pattern="[a-fA-F0-9]{64}" maxLength={64} autoComplete="off" /></Field>
    <ErrorMessage error={write.error} /><button className="button-primary" disabled={write.isPending}>Accept invitation</button>
  </form></>
}
