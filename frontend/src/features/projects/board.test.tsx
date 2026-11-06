import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import { User } from 'oidc-client-ts'
import { expect, it, vi } from 'vitest'
import type { ReactNode } from 'react'
import { AuthContext } from '../auth/AuthContext'
import { KanbanBoard } from './KanbanBoard'
import { TaskForm } from './TaskForm'
import { previewMove, type Board } from './board'

// These tests exercise the accessible move form; pointer geometry needs a real browser.
vi.mock('@dnd-kit/react', () => ({
  DragDropProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
  useDraggable: () => ({ ref: vi.fn(), handleRef: vi.fn(), isDragging: false }),
  useDroppable: () => ({ ref: vi.fn(), isDropTarget: false }),
}))
const task = { id: 'a', projectId: 'p', workspaceId: 'w', projectKey: 'DEV', number: 1, title: 'First task',
  description: null, status: 'ToDo', priority: 'High', dueDate: null, assigneeUserId: null,
  assigneeName: null, version: 42, updatedAt: '2026-11-01T00:00:00Z' }
const board: Board = { revision: 'a'.repeat(64), cards: [{ task, position: 1 }, { task: { ...task, id: 'b', number: 2, title: 'Second task' }, position: 2 }] }
function Wrapper({ children }: { children: ReactNode }) {
  const user = new User({ access_token: 'test', token_type: 'Bearer', profile: { sub: 'user', iss: 'test', aud: 'test', exp: 9999999999, iat: 0 } })
  const auth = { user, loading: false, configured: true, error: null, signIn: vi.fn(), signOut: vi.fn(), completeSignIn: vi.fn() }
  return <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><AuthContext.Provider value={auth}><MemoryRouter>{children}</MemoryRouter></AuthContext.Provider></QueryClientProvider>
}
it('previews cross-column moves and reordering without mutating the server snapshot', () => {
  const moved = previewMove(board, { taskId: 'b', status: 'ToDo', beforeTaskId: 'a' })
  expect(moved.cards.map(c => c.task.id)).toEqual(['b', 'a'])
  expect(board.cards.map(c => c.task.id)).toEqual(['a', 'b'])
  expect(previewMove(board, { taskId: 'a', status: 'Completed', beforeTaskId: null }).cards.find(c => c.task.id === 'a')?.task.status).toBe('Completed')
})
it('moves using labelled controls and sends the snapshot revision', async () => {
  const moved = previewMove(board, { taskId: 'a', status: 'Completed', beforeTaskId: null })
  let current = board
  const fetchMock = vi.fn((_url, options) => {
    if (options.method === 'PUT') current = { ...moved, revision: 'b'.repeat(64) }
    return Promise.resolve(Response.json(current))
  })
  vi.stubGlobal('fetch', fetchMock)
  render(<KanbanBoard path="/projects/p" canWrite />, { wrapper: Wrapper })
  await screen.findByRole('link', { name: 'First task' })
  await userEvent.click(screen.getAllByText('Move task')[0])
  await userEvent.selectOptions(screen.getByLabelText('Column for DEV-1'), 'Completed')
  await userEvent.click(screen.getByRole('button', { name: 'Move DEV-1' }))
  await waitFor(() => expect(within(screen.getByRole('region', { name: 'Completed' })).getByRole('link', { name: 'First task' })).toBeInTheDocument())
  expect(fetchMock).toHaveBeenCalledWith('/api/projects/p/board/a', expect.objectContaining({ method: 'PUT', body: JSON.stringify({ status: 'Completed', beforeTaskId: null, revision: board.revision }) }))
})
it('rolls back a failed optimistic move and reloads authoritative state', async () => {
  vi.stubGlobal('fetch', vi.fn((_url, options) => Promise.resolve(options.method === 'PUT'
    ? Response.json({ title: 'The board changed. Reload it and try your move again.' }, { status: 409 }) : Response.json(board))))
  render(<KanbanBoard path="/projects/p" canWrite />, { wrapper: Wrapper })
  await screen.findByRole('link', { name: 'First task' })
  await userEvent.click(screen.getAllByText('Move task')[0])
  await userEvent.selectOptions(screen.getByLabelText('Column for DEV-1'), 'Completed')
  await userEvent.click(screen.getByRole('button', { name: 'Move DEV-1' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('The board changed')
  expect(within(screen.getByRole('region', { name: 'To Do' })).getByRole('link', { name: 'First task' })).toBeInTheDocument()
})
it('hides all move controls on a read-only board', async () => {
  vi.stubGlobal('fetch', vi.fn().mockImplementation(() => Promise.resolve(Response.json(board))))
  render(<KanbanBoard path="/projects/p" canWrite={false} />, { wrapper: Wrapper })
  await screen.findByRole('link', { name: 'First task' })
  expect(screen.queryByText('Move task')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /Drag/ })).not.toBeInTheDocument()
})
it('preserves a draft and its original version after a real-time refetch', async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json({ title: 'Conflict' }, { status: 409 }))
  vi.stubGlobal('fetch', fetchMock)
  const view = render(<TaskForm path="/tasks/a" initial={task} members={[]} />, { wrapper: Wrapper })
  await userEvent.clear(screen.getByLabelText('Title'))
  await userEvent.type(screen.getByLabelText('Title'), 'Unsaved draft')
  view.rerender(<TaskForm path="/tasks/a" initial={{ ...task, title: 'Someone else edited', version: 43 }} members={[]} />)
  expect(screen.getByLabelText('Title')).toHaveValue('Unsaved draft')
  expect(screen.getByRole('status')).toHaveTextContent('Your draft is preserved')
  await userEvent.click(screen.getByRole('button', { name: 'Save task' }))
  await screen.findByRole('alert')
  expect(JSON.parse(fetchMock.mock.calls[0][1].body).version).toBe(42)
})
