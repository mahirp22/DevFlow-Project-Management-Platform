import { Component, type ReactNode } from 'react'
import { Link } from 'react-router'

// Route-keyed by the layout so navigation can recover without reloading the shell.
export class PageBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() { return { failed: true } }

  render() {
    if (!this.state.failed) return this.props.children
    return <section className="empty-state" role="alert">
      <h1>This page couldn't load.</h1>
      <p>Check your connection and reload, or return to the overview. Unsaved edits may be lost when reloading.</p>
      <button className="button-primary" onClick={() => window.location.reload()}>Reload page</button>
      <Link className="text-link" to="/">Back to overview</Link>
    </section>
  }
}
