import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { requestJson } from '../../lib/api'
import { list, useData } from './data'
import { attachment, attachmentConfiguration, downloadGrant, fileChecksum, fileContentType, sendToStorage, uploadGrant } from './attachments'
import { ErrorMessage } from './ui'

export function AttachmentPanel({ path, canWrite }: { path: string; canWrite: boolean }) {
  const { user } = useAuth()
  const cache = useQueryClient()
  const configuration = useData('/attachments/configuration', attachmentConfiguration)
  const files = useData(path, list(attachment))
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState<Error | null>(null)
  const cancellation = useRef<AbortController | null>(null)
  useEffect(() => () => cancellation.current?.abort(), [])
  async function perform(operation: (signal: AbortSignal) => Promise<void>) {
    if (busy) return
    const controller = new AbortController(); cancellation.current = controller
    setBusy(true); setError(null); setMessage('')
    try { await operation(controller.signal) }
    catch (cause) { if (!controller.signal.aborted) { setError(cause instanceof Error ? cause : new Error('Attachment operation failed.')); setMessage('') } }
    finally { setBusy(false); await cache.invalidateQueries({ queryKey: ['app-data'] }) }
  }
  return <section className="editor-card mt-7"><h2>Attachments</h2>
    <ErrorMessage error={error ?? configuration.error ?? files.error} />
    {configuration.data && !configuration.data.configured && <p className="my-4 text-sm text-slate-600">File uploads are not configured for this installation yet.</p>}
    {canWrite && configuration.data?.configured && <form className="my-5" onSubmit={event => {
      event.preventDefault()
      const form = event.currentTarget
      const file = (form.elements.namedItem('file') as HTMLInputElement).files?.[0]
      if (!file) return
      void perform(async signal => {
        const contentType = fileContentType(file)
        setMessage('Preparing file…')
        const checksum = await fileChecksum(file)
        const started = uploadGrant(await requestJson(path, { method: 'POST', token: user?.access_token, signal,
          body: { fileName: file.name, contentType, size: file.size, checksum } }))
        setMessage('Uploading file…')
        await sendToStorage(file, started.upload, signal)
        setMessage('Verifying file…')
        await requestJson(path + '/' + started.id + '/finalize', { method: 'POST', token: user?.access_token, signal })
        form.reset(); setMessage('File attached.')
      })
    }}>
      <label className="field"><span>Choose attachment</span><input name="file" type="file" required accept=".pdf,.png,.jpg,.jpeg,.txt" disabled={busy} /></label>
      <p className="mb-3 text-xs text-slate-500">PDF, PNG, JPEG or TXT · Up to 10 MiB each · 20 files per task</p>
      <button className="button-secondary" disabled={busy}>Upload attachment</button>
    </form>}
    <p role="status" aria-live="polite" className="my-3 text-sm">{message}</p>
    {files.isLoading && <p>Loading attachments…</p>}
    {files.data?.length === 0 && <p className="my-3 text-sm text-slate-500">No attachments yet.</p>}
    {!files.isError && files.data?.map(file => <div className="member-row" key={file.id}><div className="min-w-0"><strong className="break-words">{file.fileName}</strong><p>{(file.size / 1024).toFixed(1)} KiB · {file.uploader}{!file.ready && ' · Awaiting verification'}</p></div>
      {file.ready ? <button className="button-secondary" disabled={busy || !configuration.data?.configured} onClick={() => void perform(async signal => {
        const grant = downloadGrant(await requestJson(path + '/' + file.id + '/download', { method: 'POST', token: user?.access_token, signal }))
        window.location.assign(grant.url)
      })}>Download</button> : canWrite && <button className="button-secondary" disabled={busy} onClick={() => void perform(async signal => {
        await requestJson(path + '/' + file.id + '/finalize', { method: 'POST', token: user?.access_token, signal }); setMessage('File attached.')
      })}>Finish upload</button>}
    </div>)}
    {files.data?.some(file => !file.ready) && <p className="mt-3 text-xs text-slate-500">If the file reached storage but verification was interrupted, choose Finish upload. Otherwise choose the file again. Unfinished entries expire after 15 minutes.</p>}
  </section>
}
