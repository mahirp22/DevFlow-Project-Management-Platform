import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { DragDropProvider, useDraggable, useDroppable } from '@dnd-kit/react'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { requestJson } from '../../lib/api'
import { label, projectUrl, statuses, useData, type Task } from './data'
import { parseBoard, previewMove, type Board, type Move } from './board'
import { ErrorMessage } from './ui'

export function KanbanBoard({ path, canWrite }: { path: string; canWrite: boolean }) {
  const query = useData(path + '/board', parseBoard)
  const { user } = useAuth()
  const cache = useQueryClient()
  const [preview, setPreview] = useState<Board | null>(null)
  const [error, setError] = useState<Error | null>(null)
  const [message, setMessage] = useState('')
  const busy = useRef(false)
  const dragSnapshot = useRef<Board | null>(null)
  const board = preview ?? query.data
  async function move(request: Move, snapshot = query.data) {
    if (!snapshot || !canWrite || busy.current) return
    busy.current = true
    setError(null); setMessage('Saving move…'); setPreview(previewMove(snapshot, request))
    try {
      const saved = parseBoard(await requestJson(path + '/board/' + request.taskId, { method: 'PUT', token: user?.access_token,
        body: { status: request.status, beforeTaskId: request.beforeTaskId, revision: snapshot.revision } }))
      cache.setQueryData(['app-data', user?.profile.sub, path + '/board'], saved)
      setMessage('Task moved.')
    } catch (cause) {
      setError(cause instanceof Error ? cause : new Error('Move failed.'))
      setMessage('Move was not saved. Reloading the board.')
    } finally {
      setPreview(null); busy.current = false
      await cache.invalidateQueries({ queryKey: ['app-data'] })
    }
  }
  if (query.isLoading) return <p role="status">Loading board…</p>
  if (query.error) return <><ErrorMessage error={query.error} /><button className="button-secondary" onClick={() => void query.refetch()}>Reload board</button></>
  return <>
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3"><p className="text-sm text-slate-600">{canWrite ? 'Drag by the handle. Drop before a card or at the end of a column. You can also use each card’s Move controls.' : 'Read-only board.'}</p><button className="button-secondary" disabled={Boolean(preview)} onClick={() => void query.refetch()}>Reload board</button></div>
    <p role="status" aria-live="polite" className="mb-2 text-sm">{message}</p><ErrorMessage error={error} />
    <DragDropProvider onDragStart={() => { dragSnapshot.current = query.data ?? null }} onDragEnd={event => {
      const snapshot = dragSnapshot.current; dragSnapshot.current = null
      if (event.canceled || !snapshot) return
      const { source, target } = event.operation
      if (!source || !target) return
      const destination = String(target.id).split(':')
      if (destination[0] === 'end') void move({ taskId: String(source.id), status: destination[1], beforeTaskId: null }, snapshot)
      if (destination[0] === 'before' && destination[1] !== source.id) {
        const before = snapshot.cards.find(c => c.task.id === destination[1])
        if (before) void move({ taskId: String(source.id), status: before.task.status, beforeTaskId: before.task.id }, snapshot)
      }
    }}>
      <div className="kanban-board">{statuses.map(status => {
        const cards = board?.cards.filter(c => c.task.status === status).sort((a, b) => a.position - b.position || a.task.number - b.task.number) ?? []
        return <section className="kanban-column" key={status} aria-label={label(status)}><h2 className="mb-4 flex justify-between text-sm font-semibold">{label(status)}<span className="subtle-tag">{cards.length}</span></h2>
          {cards.map(c => <BoardTask key={c.task.id} task={c.task} board={board!} enabled={canWrite && !preview} onMove={request => void move(request)} />)}
          <DropTarget id={'end:' + status} enabled={canWrite && !preview} text={cards.length ? 'Drop at end' : 'No tasks yet — drop here'} />
        </section>
      })}</div>
    </DragDropProvider>
  </>
}
function DropTarget({ id, enabled, text }: { id: string; enabled: boolean; text: string }) {
  const { ref, isDropTarget } = useDroppable({ id, disabled: !enabled })
  return <div ref={ref} className={'board-drop ' + (isDropTarget ? 'board-drop-active' : '')}>{text}</div>
}
function BoardTask({ task, board, enabled, onMove }: { task: Task; board: Board; enabled: boolean; onMove: (move: Move) => void }) {
  const { ref, handleRef, isDragging } = useDraggable({ id: task.id, disabled: !enabled })
  const [destination, setDestination] = useState(task.status)
  return <>
    <DropTarget id={'before:' + task.id} enabled={enabled} text={'Before ' + task.projectKey + '-' + task.number} />
    <article ref={ref} className={'board-card ' + (isDragging ? 'opacity-50' : '')}>
      <div className="flex items-center justify-between gap-2"><span className="text-xs text-slate-500">{task.projectKey}-{task.number}</span>{enabled && <button ref={handleRef} className="drag-handle" aria-label={'Drag ' + task.title}>⠿</button>}</div>
      <Link className="my-3 block font-semibold" to={projectUrl(task.workspaceId, task.projectId) + '/tasks/' + task.id}>{task.title}</Link>
      <div className="flex flex-wrap gap-2 text-xs"><span className="subtle-tag">{task.priority}</span><span>{task.assigneeName ?? 'Unassigned'}</span></div>
      {task.dueDate && <p className="mt-3 text-xs text-slate-500">Due {task.dueDate}</p>}
      {enabled && <details className="mt-4 text-xs"><summary>Move task</summary><form className="mt-3" onSubmit={event => {
        event.preventDefault(); const values = new FormData(event.currentTarget)
        onMove({ taskId: task.id, status: destination, beforeTaskId: String(values.get('before') ?? '') || null })
      }}><label className="field"><span>Column for {task.projectKey}-{task.number}</span><select value={destination} onChange={e => setDestination(e.target.value)}>{statuses.map(s => <option key={s} value={s}>{label(s)}</option>)}</select></label>
        <label className="field"><span>Position for {task.projectKey}-{task.number}</span><select name="before" key={destination} defaultValue=""><option value="">End of column</option>{board.cards.filter(c => c.task.status === destination && c.task.id !== task.id).map(c => <option key={c.task.id} value={c.task.id}>Before {c.task.projectKey}-{c.task.number}</option>)}</select></label>
        <button className="button-secondary">Move {task.projectKey}-{task.number}</button>
      </form></details>}
    </article>
  </>
}
