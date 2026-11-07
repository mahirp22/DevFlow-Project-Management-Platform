import { lazy, Suspense } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { expect, it, vi } from 'vitest'
import { PageBoundary } from './PageBoundary'

it('recovers from a failed page download through navigation without exposing the error', async () => {
  const BrokenPage = lazy(() => Promise.reject(new Error('private diagnostic detail')))
  function Layout() {
    const location = useLocation()
    return <PageBoundary key={location.pathname}>
      <Suspense fallback={<p role="status">Loading page…</p>}>
        <Routes>
          <Route path="/broken" element={<BrokenPage />} />
          <Route path="/" element={<h1>Overview restored</h1>} />
        </Routes>
      </Suspense>
    </PageBoundary>
  }
  const errors = vi.spyOn(console, 'error').mockImplementation(() => {})
  try {
    render(<MemoryRouter initialEntries={['/broken']}><Layout /></MemoryRouter>)
    expect(await screen.findByRole('alert')).toHaveTextContent("This page couldn't load.")
    expect(screen.queryByText('private diagnostic detail')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reload page' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Back to overview' }))
    expect(await screen.findByRole('heading', { name: 'Overview restored' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  } finally { errors.mockRestore() }
})
