import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router'
import { User } from 'oidc-client-ts'
import { expect, it, vi } from 'vitest'
import { AuthContext } from '../auth/AuthContext'
import { TaskForm } from './TaskForm'
import { InvitationPage } from './InvitationPage'
import { requestJson } from '../../lib/api'
import type { ReactNode } from 'react'

const initial = { id: 'task', projectId: 'project', workspaceId: 'workspace', projectKey: 'DEV', number: 1,
  title: 'Original title', description: null, status: 'ToDo', priority: 'Medium', dueDate: null,
  assigneeUserId: null, assigneeName: null, version: 42, updatedAt: '2026-11-01T00:00:00Z' }
function mount(children: ReactNode) {
  const user = new User({ access_token: 'test-only', token_type: 'Bearer', profile: { sub: 'test-user', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 } })
  const auth = { user, loading: false, configured: true, error: null, signIn: vi.fn(), signOut: vi.fn(), completeSignIn: vi.fn() }
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><AuthContext.Provider value={auth}><MemoryRouter>{children}</MemoryRouter></AuthContext.Provider></QueryClientProvider>)
}

it('sends the task version and preserves the edited draft when another user has saved first', async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json({ title: 'This task changed. Reload it before saving again.' }, { status: 409 }))
  vi.stubGlobal('fetch', fetchMock)
  mount(<TaskForm path="/tasks/task" initial={initial} members={[]} />)
  await userEvent.clear(screen.getByLabelText('Title'))
  await userEvent.type(screen.getByLabelText('Title'), 'My unsaved edit')
  await userEvent.click(screen.getByRole('button', { name: 'Save task' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('This task changed')
  expect(screen.getByLabelText('Title')).toHaveValue('My unsaved edit')
  expect(fetchMock).toHaveBeenCalledWith('/api/tasks/task', expect.objectContaining({ method: 'PUT',
    body: JSON.stringify({ title: 'My unsaved edit', description: null, status: 'ToDo', priority: 'Medium', dueDate: null, assigneeUserId: null, version: 42 }) }))
})

it('creates a task with its selected assignee and deadline and reports success', async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json(initial, { status: 201 }))
  vi.stubGlobal('fetch', fetchMock)
  const onSaved = vi.fn()
  mount(<TaskForm path="/tasks" members={[{ userId: 'member', displayName: 'Alex', email: 'alex@example.invalid', role: 'Member' }]} onSaved={onSaved} />)
  await userEvent.type(screen.getByLabelText('Title'), 'Ship it')
  await userEvent.selectOptions(screen.getByLabelText('Assignee'), 'member')
  await userEvent.type(screen.getByLabelText('Due date'), '2026-12-01')
  await userEvent.selectOptions(screen.getByLabelText('Priority'), 'High')
  await userEvent.click(screen.getByRole('button', { name: 'Create task' }))
  expect(await screen.findByRole('status')).toHaveTextContent('Task saved')
  expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toMatchObject({ title: 'Ship it', assigneeUserId: 'member', dueDate: '2026-12-01', priority: 'High' })
  expect(onSaved).toHaveBeenCalledWith(initial)
})

it('accepts an invitation by POST body and navigates to the returned project', async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json({ id: 'p', workspaceId: 'w', key: 'DEV', name: 'DevFlow', description: null, isArchived: false }))
  vi.stubGlobal('fetch', fetchMock)
  mount(<Routes><Route path="/" element={<InvitationPage />} /><Route path="/projects/w/p" element={<h1>Joined project</h1>} /></Routes>)
  await userEvent.type(screen.getByLabelText('Invitation code'), 'a'.repeat(64))
  await userEvent.click(screen.getByRole('button', { name: 'Accept invitation' }))
  expect(await screen.findByRole('heading', { name: 'Joined project' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/api/invitations/accept', expect.objectContaining({ method: 'POST', body: JSON.stringify({ token: 'a'.repeat(64) }) }))
})

it('handles successful empty DELETE responses with bearer authorization', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetchMock)
  await expect(requestJson('/tasks/1?version=42', { method: 'DELETE', token: 'test-only' })).resolves.toBeUndefined()
  await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/tasks/1?version=42', expect.objectContaining({ method: 'DELETE', headers: { Accept: 'application/json', Authorization: 'Bearer test-only' } })))
})
