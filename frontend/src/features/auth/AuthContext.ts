import { createContext, useContext } from 'react'
import type { User } from 'oidc-client-ts'

export interface AuthState {
  configured: boolean
  loading: boolean
  user: User | null
  error: string | null
  signIn: (returnTo?: string) => Promise<void>
  signOut: () => Promise<void>
  completeSignIn: (url: string) => Promise<string>
}

export const AuthContext = createContext<AuthState | null>(null)
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('AuthProvider is required.')
  return context
}
