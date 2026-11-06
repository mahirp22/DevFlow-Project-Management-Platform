import type { FormEvent } from 'react'
export function formData(event: FormEvent<HTMLFormElement>) { event.preventDefault(); return new FormData(event.currentTarget) }
export const text = (form: FormData, key: string) => String(form.get(key) ?? '').trim()
