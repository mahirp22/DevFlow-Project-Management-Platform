import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { QueryClientProvider } from '@tanstack/react-query'
import { createQueryClient } from './lib/queryClient'
import './index.css'
import App from './App'
import { AuthProvider } from './features/auth/AuthProvider'

const queryClient = createQueryClient()
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider><BrowserRouter><App /></BrowserRouter></AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
)
