import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import App from './App'
import { createQueryClient } from './lib/queryClient'

function renderApp(path = '/') {
  return render(<QueryClientProvider client={createQueryClient()}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></QueryClientProvider>)
}

describe('workspace navigation', () => {
  it('navigates from the overview to projects and updates focus and title', async () => {
    renderApp()
    await userEvent.click(screen.getByRole('link', { name: /explore projects/i }))
    expect(screen.getByRole('heading', { level: 1, name: 'Projects' })).toBeInTheDocument()
    expect(screen.getByRole('main')).toHaveFocus()
    expect(document.title).toBe('Projects · DevFlow')
    expect(screen.getByRole('link', { name: 'Projects' })).toHaveAttribute('aria-current', 'page')
  })

  it('renders a nested link directly and provides a way back from unknown URLs', async () => {
    renderApp('/missing')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('This page took a different path.')
    await userEvent.click(screen.getByRole('link', { name: 'Back to overview' }))
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Room for your next big idea.')
  })

  it('closes the mobile menu after navigation', async () => {
    renderApp()
    const toggle = screen.getByRole('button', { name: 'Menu' })
    await userEvent.click(toggle)
    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    await userEvent.click(screen.getByRole('link', { name: 'My tasks' }))
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    expect(screen.getByRole('heading', { level: 1, name: 'My tasks' })).toBeInTheDocument()
  })
})

describe('connection checks', () => {
  it('waits for user action, shows pending state, and checks the API only', async () => {
    let resolve!: (value: Response) => void
    const fetchMock = vi.fn(() => new Promise<Response>(done => { resolve = done }))
    vi.stubGlobal('fetch', fetchMock)
    renderApp('/connection')
    expect(fetchMock).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Check api' }))
    expect(screen.getByRole('button', { name: 'Checking…' })).toBeDisabled()
    resolve(Response.json({ status: 'Healthy' }))
    expect(await screen.findByText('Connected and healthy.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/health', expect.objectContaining({ signal: expect.any(AbortSignal) }))
    expect(screen.getByText('Ready to check.')).toBeInTheDocument()
  })

  it('recovers from an unavailable database after a retry', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(Response.json({ title: 'Unavailable', traceId: 'trace-123' }, { status: 503 })).mockResolvedValueOnce(Response.json({ status: 'Healthy' }))
    vi.stubGlobal('fetch', fetchMock)
    renderApp('/connection')
    await userEvent.click(screen.getByRole('button', { name: 'Check database' }))
    expect(await screen.findByText(/Unable to connect/)).toBeInTheDocument()
    expect(screen.getByText('Reference: trace-123')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry database' }))
    expect(await screen.findByText('Connected and healthy.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(fetchMock).toHaveBeenLastCalledWith('/api/health/ready', expect.any(Object))
  })

  it('does not mistake an unexpected payload for a healthy service', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ status: 'Unhealthy' })))
    renderApp('/connection')
    await userEvent.click(screen.getByRole('button', { name: 'Check api' }))
    await waitFor(() => expect(screen.getByText(/Unable to connect/)).toBeInTheDocument())
  })
})
