export const maximumFileBytes = 10 * 1024 * 1024
const contentTypes: Record<string, string> = { pdf: 'application/pdf', png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', txt: 'text/plain' }
export function fileContentType(file: File) {
  const type = contentTypes[file.name.split('.').at(-1)?.toLowerCase() ?? '']
  if (!type || file.name.length > 180 || ([...file.name].some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) < 160)) || /[/\\:*?"<>|]/.test(file.name)) || file.name !== file.name.trim()) throw new Error('Choose a PDF, PNG, JPEG or TXT file with a simple filename.')
  if (file.size < 1 || file.size > maximumFileBytes) throw new Error('Choose a non-empty file no larger than 10 MiB.')
  return type
}
export async function fileChecksum(file: File) {
  const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer())
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
}
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object') throw new Error('Invalid attachment response.')
  return value as Record<string, unknown>
}
function string(value: unknown): string { if (typeof value !== 'string') throw new Error('Invalid attachment response.'); return value }
export function attachment(value: unknown) {
  const x = object(value)
  if (typeof x.size !== 'number' || typeof x.ready !== 'boolean') throw new Error('Invalid attachment response.')
  return { id: string(x.id), fileName: string(x.fileName), contentType: string(x.contentType), size: x.size,
    ready: x.ready, uploader: string(x.uploader), createdAt: string(x.createdAt) }
}
export function attachmentConfiguration(value: unknown) {
  const x = object(value)
  if (typeof x.configured !== 'boolean') throw new Error('Invalid attachment response.')
  return { configured: x.configured }
}
export function downloadGrant(value: unknown) {
  const x = object(value), url = string(x.url)
  if (new URL(url).protocol !== 'https:') throw new Error('Invalid storage URL.')
  return { url, expiresAt: string(x.expiresAt) }
}
export function uploadGrant(value: unknown) {
  const x = object(value), upload = object(x.upload), fields = object(upload.fields)
  const parsed: Record<string, string> = {}
  for (const [key, value] of Object.entries(fields)) parsed[key] = string(value)
  return { id: string(x.id), upload: { ...downloadGrant(upload), fields: parsed } }
}
export async function sendToStorage(file: File, upload: ReturnType<typeof uploadGrant>['upload'], signal: AbortSignal) {
  const form = new FormData()
  for (const [name, value] of Object.entries(upload.fields)) form.append(name, value)
  form.append('file', file) // S3 requires the file field last; the browser sets the multipart boundary.
  const response = await fetch(upload.url, { method: 'POST', body: form, credentials: 'omit', referrerPolicy: 'no-referrer',
    signal: AbortSignal.any([signal, AbortSignal.timeout(120_000)]) })
  if (!response.ok) throw new Error('The file upload failed. Check your connection and retry with a new upload.')
}
