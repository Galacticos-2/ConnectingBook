// Tokens stay in this worker's memory and are scoped to the browser client that supplied them.
const tokens = new Map()
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()))
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()))
self.addEventListener('message', event => {
  const source = event.source
  if (!source?.id || new URL(source.url).origin !== self.location.origin || event.data?.type !== 'auth') return
  if (typeof event.data.token === 'string' && event.data.token.length < 8192) tokens.set(source.id, event.data.token)
  else tokens.delete(source.id)
  event.ports[0]?.postMessage({ ready: true })
})
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url)
  const protectedPath = /^\/api\/media\/[0-9a-f-]+$/i.test(url.pathname) || /^\/api\/admin\/reports\/\d+\/media\/[0-9a-f-]+$/i.test(url.pathname)
  if (url.origin !== self.location.origin || !protectedPath || event.request.method !== 'GET') return
  if (event.request.headers.has('Authorization')) return
  event.respondWith((async () => {
    const token = tokens.get(event.clientId)
    const client = await self.clients.get(event.clientId)
    if (!token) {
      client?.postMessage({ type: 'media-auth-needed' })
      return new Response(null, { status: 401 })
    }
    const headers = new Headers(event.request.headers)
    headers.set('Authorization', `Bearer ${token}`)
    // Preserve Range/If-Range and stream the response without caching private bytes in the worker.
    // Native video requests use no-cors, whose header guard removes Authorization.
    // The route was checked above, so an explicit same-origin request can carry the token.
    const response = await fetch(new Request(event.request, { headers, mode: 'same-origin', credentials: 'same-origin' }))
    if (response.status === 401) client?.postMessage({ type: 'media-auth-needed' })
    return response
  })())
})
