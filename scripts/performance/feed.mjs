import os from 'node:os'
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'

const base = process.env.PERF_ORIGIN ?? 'https://localhost:7244'
const email = process.env.PERF_EMAIL ?? process.env.DEMO_EMAIL
const password = process.env.PERF_PASSWORD ?? process.env.DEMO_PASSWORD
if (!email || !password) throw new Error('Provide PERF_EMAIL/PERF_PASSWORD through the environment. No credentials are printed.')
const login = await fetch(`${base}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }) })
if (!login.ok) throw new Error(`Benchmark login failed with HTTP ${login.status}`)
const { accessToken } = await login.json()
const headers = { Authorization: `Bearer ${accessToken}` }
const samples = []
const concurrency = 5
const requestsPerWorker = 20
const warmup = 15
for (let index = 0; index < warmup; index++) { const response = await fetch(`${base}/api/feed?limit=20`, { headers }); if (!response.ok) throw new Error(`Warmup failed: HTTP ${response.status}`); await response.arrayBuffer() }
await Promise.all(Array.from({ length: concurrency }, async () => {
  for (let index = 0; index < requestsPerWorker; index++) {
    const start = performance.now()
    const response = await fetch(`${base}/api/feed?limit=20`, { headers })
    if (!response.ok) throw new Error(`Benchmark failed: HTTP ${response.status}`)
    const body = await response.json()
    if (!Array.isArray(body.items) || body.items.length > 20) throw new Error('Invalid feed response')
    samples.push(performance.now() - start)
  }
}))
samples.sort((left, right) => left - right)
const result = {
  measuredAtUtc: new Date().toISOString(), origin: base, runtime: process.version,
  platform: `${os.platform()} ${os.release()}`, cpu: os.cpus()[0]?.model, logicalCpus: os.cpus().length,
  memoryGiB: Math.round(os.totalmem() / 1024 ** 3), requests: samples.length, concurrency, warmup,
  p50Ms: +samples[Math.ceil(samples.length * .5) - 1].toFixed(2),
  p95Ms: +samples[Math.ceil(samples.length * .95) - 1].toFixed(2),
  maxMs: +samples.at(-1).toFixed(2), targetP95Ms: 300,
  dataset: { users: Number(process.env.PERF_USERS ?? 0), posts: Number(process.env.PERF_POSTS ?? 0), note: 'Counts must be verified against the dedicated benchmark database.' },
}
const output = resolve('docs/validation/feed-benchmark.json')
await mkdir(dirname(output), { recursive: true })
await writeFile(output, JSON.stringify(result, null, 2) + '\n')
console.log(JSON.stringify(result, null, 2))
if (result.p95Ms >= 300) process.exitCode = 1
