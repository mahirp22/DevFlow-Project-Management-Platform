import { Route, Routes } from 'react-router'
import { AppLayout } from './components/AppLayout'
import { OverviewPage, ProjectsPage, TasksPage, NotFoundPage } from './pages/WorkspacePages'
import { ConnectionPage } from './features/connection/ConnectionPage'

export default function App() {
  return <Routes>
    <Route element={<AppLayout />}>
      <Route index element={<OverviewPage />} />
      <Route path="projects" element={<ProjectsPage />} />
      <Route path="tasks" element={<TasksPage />} />
      <Route path="connection" element={<ConnectionPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Route>
  </Routes>
}