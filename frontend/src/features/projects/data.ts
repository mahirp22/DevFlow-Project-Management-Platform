import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { requestJson } from '../../lib/api'

// Validate the JSON boundary instead of asserting that unknown responses match TypeScript.
const object = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Unexpected server response.')
  return value as Record<string, unknown>
}
const str = (value: unknown): string => { if (typeof value !== 'string') throw new Error('Unexpected server response.'); return value }
const num = (value: unknown): number => { if (typeof value !== 'number') throw new Error('Unexpected server response.'); return value }
const bool = (value: unknown): boolean => { if (typeof value !== 'boolean') throw new Error('Unexpected server response.'); return value }
const nullable = <T,>(parse: (v: unknown) => T, value: unknown): T | null => value === null ? null : parse(value)
export const list = <T,>(parse: (v: unknown) => T) => (value: unknown): T[] => {
  if (!Array.isArray(value)) throw new Error('Unexpected server response.')
  return value.map(parse)
}
export const workspace = (v: unknown) => { const x = object(v); return { id: str(x.id), name: str(x.name), role: str(x.role) } }
export const project = (v: unknown) => { const x = object(v); return { id: str(x.id), workspaceId: str(x.workspaceId), key: str(x.key), name: str(x.name), description: nullable(str, x.description), isArchived: bool(x.isArchived) } }
export const member = (v: unknown) => { const x = object(v); return { userId: str(x.userId), displayName: str(x.displayName), email: str(x.email), role: str(x.role) } }
export const permissions = (v: unknown) => { const x = object(v); return { canRead: bool(x.canRead), canWrite: bool(x.canWrite), canManage: bool(x.canManage) } }
export const task = (v: unknown) => {
  const x = object(v)
  return { id: str(x.id), projectId: str(x.projectId), workspaceId: str(x.workspaceId), projectKey: str(x.projectKey),
    number: num(x.number), title: str(x.title), description: nullable(str, x.description), status: str(x.status),
    priority: str(x.priority), dueDate: nullable(str, x.dueDate), assigneeUserId: nullable(str, x.assigneeUserId),
    assigneeName: nullable(str, x.assigneeName), version: num(x.version), updatedAt: str(x.updatedAt) }
}
export const comment = (v: unknown) => { const x = object(v); return { id: str(x.id), body: str(x.body), author: str(x.author), createdAt: str(x.createdAt) } }
export const activity = (v: unknown) => { const x = object(v); return { id: str(x.id), summary: str(x.summary), actor: str(x.actor), createdAt: str(x.createdAt) } }
export const invitation = (v: unknown) => { const x = object(v); return { id: str(x.id), email: str(x.email), role: str(x.role), expiresAt: str(x.expiresAt), isRevoked: bool(x.isRevoked), acceptedAt: nullable(str, x.acceptedAt) } }
export const invitationCreated = (v: unknown) => { const x = object(v); return { invitation: invitation(x.invitation), token: str(x.token) } }
export const page = <T,>(parse: (v: unknown) => T) => (v: unknown) => {
  const x = object(v); return { items: list(parse)(x.items), total: num(x.total), page: num(x.page), pageSize: num(x.pageSize) }
}
export const analytics = (v: unknown) => {
  const x = object(v)
  return { total: num(x.total), overdue: num(x.overdue), completed: num(x.completed),
    byStatus: list(v => { const r = object(v); return { status: str(r.status), count: num(r.count) } })(x.byStatus),
    workload: list(v => { const r = object(v); return { assignee: str(r.assignee), count: num(r.count) } })(x.workload) }
}
export type Project = ReturnType<typeof project>
export type Task = ReturnType<typeof task>
export type Member = ReturnType<typeof member>
export const statuses = ['ToDo', 'InProgress', 'InReview', 'Completed']
export const priorities = ['Low', 'Medium', 'High', 'Urgent']
export const label = (s: string) => ({ ToDo: 'To Do', InProgress: 'In Progress', InReview: 'In Review' }[s] ?? s)
export const projectPath = (workspaceId: string, projectId: string) => `/workspaces/${workspaceId}/projects/${projectId}`
export const projectUrl = (workspaceId: string, projectId: string) => `/projects/${workspaceId}/${projectId}`

export function useData<T>(path: string, parse: (v: unknown) => T, enabled = true) {
  const { user } = useAuth()
  return useQuery({ queryKey: ['app-data', user?.profile.sub, path], enabled: enabled && Boolean(user),
    queryFn: async ({ signal }) => parse(await requestJson(path, { signal, token: user?.access_token })) })
}
export function useWrite() {
  const { user } = useAuth()
  const cache = useQueryClient()
  return useMutation({
    mutationFn: (request: { path: string; method?: 'POST' | 'PUT' | 'DELETE'; body?: unknown }) =>
      requestJson(request.path, { method: request.method ?? 'POST', body: request.body, token: user?.access_token }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ['app-data'] }),
  })
}
