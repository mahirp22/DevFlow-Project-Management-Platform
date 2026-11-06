import { Heading } from './ui'
import { TaskList } from './TaskList'
export function MyTasksPage() {
  return <><Heading title="My tasks" /><p className="mb-6 text-sm text-slate-500">Your assignments across active projects.</p><TaskList path="/me/tasks" /></>
}
