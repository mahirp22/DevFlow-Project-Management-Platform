import { Route, Routes } from 'react-router'
import { AppLayout } from './components/AppLayout'
import { OverviewPage, ProjectsPage, TasksPage, NotFoundPage } from './pages/WorkspacePages'
import { ConnectionPage } from './features/connection/ConnectionPage'
import { AccountPage, CallbackPage, RequireAuth, SignInPage } from './features/auth/AuthPages'

export default function App() {
  return <Routes>
    <Route element={<AppLayout />}>
      <Route index element={<OverviewPage />} />
      <Route element={<RequireAuth />}>
        <Route path="projects" element={<ProjectsPage />} />
        <Route path="tasks" element={<TasksPage />} />
        <Route path="account" element={<AccountPage />} />
      </Route>
      <Route path="sign-in" element={<SignInPage />} />
      <Route path="auth/callback" element={<CallbackPage />} />
      <Route path="connection" element={<ConnectionPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Route>
  </Routes>
}
