import { lazy } from 'react'
import { Route, Routes } from 'react-router'
import { AppLayout } from './components/AppLayout'
import { OverviewPage, NotFoundPage } from './pages/WorkspacePages'
import { ConnectionPage } from './features/connection/ConnectionPage'
import { AccountPage, CallbackPage, RequireAuth, SignInPage } from './features/auth/AuthPages'

// Keep the overview and sign-in light; project tools load only when visited.
const ProjectsPage = lazy(() => import('./features/projects/ProjectsPage').then(module => ({ default: module.ProjectsPage })))
const ProjectPage = lazy(() => import('./features/projects/ProjectPage').then(module => ({ default: module.ProjectPage })))
const WorkspacePage = lazy(() => import('./features/projects/WorkspacePage').then(module => ({ default: module.WorkspacePage })))
const TaskPage = lazy(() => import('./features/projects/TaskPage').then(module => ({ default: module.TaskPage })))
const MyTasksPage = lazy(() => import('./features/projects/MyTasksPage').then(module => ({ default: module.MyTasksPage })))
const InvitationPage = lazy(() => import('./features/projects/InvitationPage').then(module => ({ default: module.InvitationPage })))

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
