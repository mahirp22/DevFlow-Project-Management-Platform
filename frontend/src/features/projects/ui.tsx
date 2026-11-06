import type { ReactNode } from 'react'
export function ErrorMessage({ error }: { error: Error | null }) {
  return error ? <p role="alert" className="my-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error.message}</p> : null
}
export function Heading({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="page-heading"><div><p className="eyebrow">YOUR WORKSPACE</p><h1>{title}</h1></div>{children}</div>
}
export function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="field"><span>{label}</span>{children}</label>
}
export function Pager({ page, total, pageSize, onChange }: { page: number; total: number; pageSize: number; onChange: (page: number) => void }) {
  return <div className="mt-5 flex items-center justify-between gap-3 text-sm">
    <button className="button-secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>Previous</button>
    <span>Page {page} · {total} results</span>
    <button className="button-secondary" disabled={page * pageSize >= total} onClick={() => onChange(page + 1)}>Next</button>
  </div>
}
