import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import type { ProxyOptions } from 'vite'
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const backend = process.env.API_PROXY_TARGET
  ?? process.env.ASPNETCORE_URLS?.split(';').find(url => url.startsWith('https://'))
  ?? 'https://localhost:7041'
const publicCertificate = fileURLToPath(new URL('../.local/localhost-public.pem', import.meta.url))
const developmentOrigin = 'http://localhost:5173'

function proxy(): ProxyOptions {
  return {
    target: backend,
    changeOrigin: true,
    secure: true,
    ...(existsSync(publicCertificate) ? { ca: readFileSync(publicCertificate, 'utf8') } : {}),
    configure(server) {
      server.on('proxyReq', (outgoing, request) => {
        // Rewrite only our own development origin. Other origins remain subject to API validation.
        if (request.headers.origin === developmentOrigin) outgoing.setHeader('Origin', new URL(backend).origin)
      })
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': proxy(),
      '/openapi': proxy(),
      '/swagger': proxy(),
      '/health': proxy(),
    },
  },
})
