import { Route, Routes } from 'react-router'
import { AppLayout } from './components/AppLayout'
import { OverviewPage, NotFoundPage } from './pages/WorkspacePages'
import { ProjectsPage } from './features/projects/ProjectsPage'
import { ProjectPage } from './features/projects/ProjectPage'
import { WorkspacePage } from './features/projects/WorkspacePage'
import { TaskPage } from './features/projects/TaskPage'
import { MyTasksPage } from './features/projects/MyTasksPage'
import { InvitationPage } from './features/projects/InvitationPage'
import { ConnectionPage } from './features/connection/ConnectionPage'
import { AccountPage, CallbackPage, RequireAuth, SignInPage } from './features/auth/AuthPages'

export default function App() {
  return <Routes>
    <Route element={<AppLayout />}>
      <Route index element={<OverviewPage />} />
      <Route element={<RequireAuth />}>
        <Route path="projects" element={<ProjectsPage />} />
        <Route path="projects/:workspaceId/:projectId" element={<ProjectPage />} />
        <Route path="projects/:workspaceId/:projectId/tasks/:taskId" element={<TaskPage />} />
        <Route path="workspaces/:workspaceId" element={<WorkspacePage />} />
        <Route path="invitations" element={<InvitationPage />} />
        <Route path="tasks" element={<MyTasksPage />} />
        <Route path="account" element={<AccountPage />} />
      </Route>
      <Route path="sign-in" element={<SignInPage />} />
      <Route path="auth/callback" element={<CallbackPage />} />
      <Route path="connection" element={<ConnectionPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Route>
  </Routes>
}
