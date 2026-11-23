import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import { resolve } from 'node:path'
import { describe, expect, it, vi } from 'vitest'

type WorkerEvent = { source?: { id: string; url: string }; data?: { type: string; token: string | null }; ports?: { postMessage: (value: unknown) => void }[]; request?: Request; clientId?: string; respondWith?: (response: Promise<Response>) => void }
function worker() {
  const handlers = new Map<string, (event: WorkerEvent) => void>()
  const fetch = vi.fn().mockResolvedValue(new Response('video-bytes', { status: 206 }))
  const postMessage = vi.fn()
  const self = { location: { origin: 'https://app.test' }, addEventListener: (name: string, handler: (event: WorkerEvent) => void) => handlers.set(name, handler), clients: { get: async () => ({ postMessage }), claim: vi.fn() }, skipWaiting: vi.fn() }
  vm.runInNewContext(readFileSync(resolve(process.cwd(), 'public/media-worker.js'), 'utf8'), { self, Map, URL, Headers, Request, Response, fetch })
  return { handlers, fetch, postMessage }
}

describe('private video transport', () => {
  it('forwards the Range header with only that client’s in-memory token', async () => {
    const { handlers, fetch } = worker()
    const token = crypto.randomUUID()
    handlers.get('message')!({ source: { id: 'viewer-a', url: 'https://app.test/' }, data: { type: 'auth', token }, ports: [] })
    let response: Promise<Response> | undefined
    handlers.get('fetch')!({ request: new Request('https://app.test/api/media/11111111-1111-1111-1111-111111111111', { mode: 'no-cors', headers: { Range: 'bytes=0-15' } }), clientId: 'viewer-a', respondWith: value => { response = value } })
    expect((await response!).status).toBe(206)
    const sent = fetch.mock.calls[0][0] as Request
    expect(sent.headers.get('Range')).toBe('bytes=0-15')
    expect(sent.headers.get('Authorization')).toBe(`Bearer ${token}`)
    expect(sent.mode).toBe('same-origin')
    expect(new URL(sent.url).search).toBe('')
  })
  it('does not share a token with another client and clears authentication on logout', async () => {
    const { handlers, fetch, postMessage } = worker()
    handlers.get('message')!({ source: { id: 'viewer-a', url: 'https://app.test/' }, data: { type: 'auth', token: crypto.randomUUID() }, ports: [] })
    let response: Promise<Response> | undefined
    const request = () => new Request('https://app.test/api/media/11111111-1111-1111-1111-111111111111')
    handlers.get('fetch')!({ request: request(), clientId: 'viewer-b', respondWith: value => { response = value } })
    expect((await response!).status).toBe(401)
    expect(fetch).not.toHaveBeenCalled()
    expect(postMessage).toHaveBeenCalledWith({ type: 'media-auth-needed' })
    handlers.get('message')!({ source: { id: 'viewer-a', url: 'https://app.test/' }, data: { type: 'auth', token: null }, ports: [] })
    handlers.get('fetch')!({ request: request(), clientId: 'viewer-a', respondWith: value => { response = value } })
    expect((await response!).status).toBe(401)
  })
  it('never intercepts another origin', () => {
    const { handlers } = worker()
    const respondWith = vi.fn()
    handlers.get('fetch')!({ request: new Request('https://external.test/api/media/abc'), clientId: 'viewer-a', respondWith })
    expect(respondWith).not.toHaveBeenCalled()
  })
})
