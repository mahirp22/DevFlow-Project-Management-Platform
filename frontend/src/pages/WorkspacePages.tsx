import { Link } from 'react-router'

export function OverviewPage() {
  return <>
    <div className="page-heading"><div><p className="eyebrow">YOUR WORK, IN FOCUS</p><h1>Room for your next big idea.</h1><p>Bring people, projects, and progress together.</p></div><span className="subtle-tag">Workspace overview</span></div>
    <section className="welcome-panel" aria-labelledby="welcome-title">
      <div><span className="eyebrow">A FRESH START</span><h2 id="welcome-title">Great work starts<br />with a clear plan.</h2><p>Your workspace is taking shape. Soon, you’ll have one place to organize projects and keep your team moving.</p><Link className="button-primary" to="/projects">Explore projects <span aria-hidden="true">→</span></Link></div>
      <div className="flow-art" aria-hidden="true"><div className="flow-card"><span className="art-icon">✓</span><div><strong>One shared direction</strong><i /><i /></div></div><div className="flow-card offset"><span className="art-icon mint">↗</span><div><strong>Every step forward</strong><i /><i /></div></div><span className="art-caption">IDEA → PLAN → PROGRESS</span></div>
    </section>
    <div className="section-heading"><h2>Your workspace at a glance</h2><span>Ready when you are</span></div>
    <div className="grid gap-4 md:grid-cols-3">{[
      { title: 'Projects', text: 'A home for every initiative.', to: '/projects', icon: '▦' },
      { title: 'My tasks', text: 'Make space for what matters next.', to: '/tasks', icon: '☑' },
      { title: 'Connection', text: 'Check your connection to DevFlow.', to: '/connection', icon: '↔' },
    ].map(item => <Link className="feature-card" to={item.to} key={item.to}><span className="feature-icon" aria-hidden="true">{item.icon}</span><h3>{item.title}<span aria-hidden="true">↗</span></h3><p>{item.text}</p></Link>)}</div>
    <section className="getting-started"><div className="step-number">01</div><div><h2>Make yourself at home</h2><p>Explore your workspace. Account access and project creation are coming in the next milestones.</p></div><Link to="/connection" className="text-link">Check connection →</Link></section>
  </>
}

function EmptyState({ title, description, icon }: { title: string; description: string; icon: string }) {
  return <>
    <div className="page-heading"><div><p className="eyebrow">YOUR WORKSPACE</p><h1>{title}</h1><p>{description}</p></div></div>
    <section className="empty-state"><span className="empty-icon" aria-hidden="true">{icon}</span><h2>{title === 'Projects' ? 'A place for your next project' : 'A clearer view of your day'}</h2><p>{title === 'Projects' ? 'Project creation will be available after account access is ready. Your team’s work will live here.' : 'Assigned tasks will appear here once project and task management are available.'}</p><span className="subtle-tag">Coming in a future milestone</span><Link to="/" className="text-link mt-6">Back to overview →</Link></section>
  </>
}
export function ProjectsPage() { return <EmptyState title="Projects" description="Turn shared goals into steady progress." icon="▦" /> }
export function TasksPage() { return <EmptyState title="My tasks" description="Your priorities, with room to focus." icon="☑" /> }
export function NotFoundPage() { return <section className="empty-state"><p className="eyebrow">404</p><h1>This page took a different path.</h1><p>The address may be incorrect or the page may have moved.</p><Link to="/" className="button-primary">Back to overview</Link></section> }