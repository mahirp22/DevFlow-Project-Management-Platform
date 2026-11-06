import { task, type Task } from './data'
export type Board = { revision: string; cards: { task: Task; position: number }[] }
export type Move = { taskId: string; status: string; beforeTaskId: string | null }
export function parseBoard(value: unknown): Board {
  if (!value || typeof value !== 'object' || !('revision' in value) || typeof value.revision !== 'string'
    || !('cards' in value) || !Array.isArray(value.cards)) throw new Error('Invalid board response.')
  return { revision: value.revision, cards: value.cards.map(v => {
    if (!v || typeof v !== 'object' || typeof v.position !== 'number') throw new Error('Invalid board card.')
    return { task: task(v.task), position: v.position }
  }) }
}
export function previewMove(board: Board, move: Move): Board {
  const moving = board.cards.find(c => c.task.id === move.taskId)
  if (!moving) return board
  const remaining = board.cards.filter(c => c.task.id !== move.taskId)
  const target = remaining.filter(c => c.task.status === move.status)
  const index = move.beforeTaskId === null ? target.length : target.findIndex(c => c.task.id === move.beforeTaskId)
  if (index < 0) return board
  target.splice(index, 0, { ...moving, task: { ...moving.task, status: move.status } })
  return { ...board, cards: [...remaining.filter(c => c.task.status !== move.status), ...target.map((c, i) => ({ ...c, position: i + 1 }))] }
}
