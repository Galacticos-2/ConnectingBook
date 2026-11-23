// Read-only API routing check: invalid registration input never creates an account.
import assert from 'node:assert/strict'
import { resolve } from 'node:path'
import { mkdir, writeFile } from 'node:fs/promises'
import { createServer } from '../project-web-connectingbook.client/node_modules/vite/dist/node/index.js'
import { createServer as createHttpServer } from 'node:http'

const root = resolve('project-web-connectingbook.client')
// A temporary API stub verifies forwarding without requiring a running user app or database.
const backend = createHttpServer((request, response) => {
  const expectedOrigin = `http://localhost:${backend.address().port}`
  response.setHeader('Content-Type', 'application/problem+json')
  if (request.url === '/health/live') response.writeHead(200).end('{}')
  else if (request.url === '/api/auth/register' && request.method === 'POST') response.writeHead(400).end('{}')
  else if (request.url === '/api/auth/refresh' && request.method === 'POST') response.writeHead(request.headers.origin === expectedOrigin ? 401 : 403).end('{}')
  else response.writeHead(404).end('{}')
})
await new Promise(resolve => backend.listen(0, 'localhost', resolve))
process.env.API_PROXY_TARGET = `http://localhost:${backend.address().port}`
const server = await createServer({ root, configFile: resolve(root, 'vite.config.ts'), server: { port: 0, strictPort: false, open: false } })
try {
  await server.listen()
  const port = server.httpServer.address().port
  const base = `http://localhost:${port}`
  const health = await fetch(`${base}/health/live`)
  assert.equal(health.status, 200)
  const registration = await fetch(`${base}/api/auth/register`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' })
  assert.equal(registration.status, 400)
  const refresh = await fetch(`${base}/api/auth/refresh`, { method: 'POST', headers: { Origin: 'http://localhost:5173' } })
  assert.equal(refresh.status, 401)
  const foreign = await fetch(`${base}/api/auth/refresh`, { method: 'POST', headers: { Origin: 'https://untrusted.invalid' } })
  assert.equal(foreign.status, 403)
  const result = { checkedAtUtc: new Date().toISOString(), backend: 'temporary HTTP stub; no database access', health: health.status, invalidRegistration: registration.status, noSessionRefresh: refresh.status, foreignOrigin: foreign.status, accountCreated: false }
  await mkdir('docs/validation', { recursive: true })
  await writeFile('docs/validation/dev-proxy.json', JSON.stringify(result, null, 2) + '\n')
  console.log(JSON.stringify(result, null, 2))
} finally { await server.close(); await new Promise(resolve => backend.close(resolve)) }
