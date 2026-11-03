import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'

const navigation = [
  { to: '/', label: 'Overview', symbol: '◈' },
  { to: '/projects', label: 'Projects', symbol: '▦' },
  { to: '/tasks', label: 'My tasks', symbol: '☑' },
  { to: '/connection', label: 'Connection', symbol: '↔' },
]

export function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()
  const main = useRef<HTMLElement>(null)
  useEffect(() => {
    const title = navigation.find(item => item.to === location.pathname)?.label ?? 'Page not found'
    document.title = `${title} · DevFlow`
    main.current?.focus()
  }, [location.pathname])

  return <div className="app-shell">
    <a href="#main" className="skip-link">Skip to content</a>
    <aside className="sidebar">
      <NavLink to="/" className="brand" aria-label="DevFlow home"><span className="brand-mark">D</span>DevFlow<span className="brand-dot">.</span></NavLink>
      <button className="menu-toggle button-secondary" aria-expanded={menuOpen} aria-controls="workspace-nav" onClick={() => setMenuOpen(!menuOpen)}>Menu</button>
      <div className={`sidebar-content ${menuOpen ? 'is-open' : ''}`}>
        <div className="workspace"><span className="workspace-avatar">W</span><div><strong>Your workspace</strong><small>Getting started</small></div></div>
        <p className="nav-caption">WORKSPACE</p>
        <nav id="workspace-nav" aria-label="Workspace">
          {navigation.map(item => <NavLink key={item.to} to={item.to} end onClick={() => setMenuOpen(false)} className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}><span aria-hidden="true">{item.symbol}</span>{item.label}</NavLink>)}
        </nav>
        <div className="sidebar-note"><span className="status-dot" />A little clarity. A lot of progress.<p>A shared space for your team's next great idea.</p></div>
      </div>
    </aside>
    <div className="min-w-0">
      <header className="topbar"><span>Workspace <span className="mx-3 text-slate-300">/</span> {navigation.find(item => item.to === location.pathname)?.label ?? 'Page not found'}</span><span className="preview-badge">Local preview</span></header>
      <main id="main" ref={main} tabIndex={-1} className="page"><Outlet /></main>
    </div>
  </div>
}