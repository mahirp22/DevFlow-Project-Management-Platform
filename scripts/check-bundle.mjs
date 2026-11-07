// Checks the full static JS dependency graph, not just the entry filename.
// Dynamic route/board imports are intentionally excluded from initial download.
import { readFileSync } from 'node:fs'
import { gzipSync } from 'node:zlib'

const dist = new URL('../frontend/dist/', import.meta.url)
const manifest = JSON.parse(readFileSync(new URL('.vite/manifest.json', dist), 'utf8'))
const entries = Object.keys(manifest).filter(key => manifest[key].isEntry)
if (entries.length !== 1) throw new Error('Expected one application entry; review the initial-download budget for new entries.')
const visited = new Set()
const files = new Set()
function visit(key) {
  if (visited.has(key)) return
  visited.add(key)
  const chunk = manifest[key]
  if (!chunk) throw new Error(`Missing static import in manifest: ${key}`)
  if (chunk.file.endsWith('.js')) files.add(chunk.file)
  for (const dependency of chunk.imports ?? []) visit(dependency)
}
visit(entries[0])
let gzipBytes = 0
for (const file of files) gzipBytes += gzipSync(readFileSync(new URL(file, dist))).length
const budget = 125_000
console.log(`Initial JavaScript: ${gzipBytes} gzip bytes across ${files.size} static files (budget ${budget}).`)
if (gzipBytes > budget) throw new Error('Initial JavaScript budget exceeded. Review eager imports before raising the budget.')
