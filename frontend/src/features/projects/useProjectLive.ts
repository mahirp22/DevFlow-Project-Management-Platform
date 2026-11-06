import { useEffect, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { useAuth } from '../auth/AuthContext'

export function useProjectLive(workspaceId: string, projectId: string) {
  const { user } = useAuth()
  const cache = useQueryClient()
  const navigate = useNavigate()
  const [state, setState] = useState('Connecting')
  useEffect(() => {
    if (!user || !workspaceId || !projectId) return
    let disposed = false
    let retry: ReturnType<typeof setTimeout> | undefined
    let refresh: ReturnType<typeof setTimeout> | undefined
    const invalidate = () => {
      if (disposed || refresh) return
      refresh = setTimeout(() => { refresh = undefined; void cache.invalidateQueries({ queryKey: ['app-data'] }) }, 100)
    }
    const base = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '')
    const connection = new HubConnectionBuilder().withUrl(base + '/hubs/projects', {
      accessTokenFactory: () => { if (user.expired) throw new Error('Session expired'); return user.access_token },
    }).configureLogging(LogLevel.None).withAutomaticReconnect([0, 2000, 5000, 10000]).build()
    const subscribe = async () => {
      await connection.invoke('Subscribe', workspaceId, projectId)
      if (!disposed) { setState('Live'); invalidate() }
    }
    const start = async () => {
      clearTimeout(retry); retry = undefined
      try { await connection.start(); if (!disposed) await subscribe() }
      catch {
        if (!disposed) {
          setState('Offline — checking every 30 seconds')
          await connection.stop()
          clearTimeout(retry)
          retry = setTimeout(() => void start(), 5000)
        }
      }
    }
    connection.on('ProjectChanged', invalidate)
    connection.on('AccessRevoked', () => {
      if (disposed) return
      cache.removeQueries({ queryKey: ['app-data'] })
      void navigate('/projects', { replace: true })
    })
    connection.onreconnecting(() => { if (!disposed) setState('Reconnecting') })
    connection.onreconnected(() => { void subscribe().catch(() => { if (!disposed) { cache.removeQueries({ queryKey: ['app-data'] }); void navigate('/projects', { replace: true }) } }) })
    connection.onclose(() => { if (!disposed) setState('Offline — checking every 30 seconds') })
    void start()
    // Recover missed best-effort notifications, including after a server restart.
    const timer = setInterval(() => { if (document.visibilityState === 'visible') invalidate() }, 30_000)
    window.addEventListener('focus', invalidate)
    return () => {
      disposed = true
      clearTimeout(retry); clearTimeout(refresh); clearInterval(timer)
      window.removeEventListener('focus', invalidate)
      void connection.stop()
    }
  }, [user, workspaceId, projectId, cache, navigate])
  return state
}
