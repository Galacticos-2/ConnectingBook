import { useEffect, useRef, useState } from 'react'
import { api, getSession, message } from '../api/client'

export function ApiDocsPage() {
  const [html, setHtml] = useState('')
  const [error, setError] = useState('')
  const frame = useRef<HTMLIFrameElement>(null)
  function authorize() {
    const view = frame.current?.contentWindow as (Window & { ui?: { preauthorizeApiKey: (name: string, token: string) => void } }) | null
    const token = getSession()?.accessToken
    if (view?.ui && token) view.ui.preauthorizeApiKey('Bearer', token)
  }
  useEffect(() => {
    const controller = new AbortController()
    const urls: string[] = []
    async function load() {
      const document = await api<unknown>('/openapi/v1.json', { signal: controller.signal })
      async function asset(path: string) {
        const response = await fetch(path, { signal: controller.signal, headers: { Authorization: `Bearer ${getSession()!.accessToken}` } })
        if (!response.ok) throw new Error('Không tải được tài liệu API.')
        const url = URL.createObjectURL(await response.blob()); urls.push(url); return url
      }
      const [css, bundle, preset] = await Promise.all(['/swagger/swagger-ui.css', '/swagger/swagger-ui-bundle.js', '/swagger/swagger-ui-standalone-preset.js'].map(asset))
      const safeSpec = JSON.stringify(document).replace(/</g, '\\u003c')
      setHtml(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><link rel="stylesheet" href="${css}"></head><body><div id="swagger-ui"></div><script src="${bundle}"></script><script src="${preset}"></script><script>window.ui=SwaggerUIBundle({spec:${safeSpec},dom_id:'#swagger-ui',presets:[SwaggerUIBundle.presets.apis,SwaggerUIStandalonePreset],persistAuthorization:false});</script></body></html>`)
    }
    load().catch(error => { if (!controller.signal.aborted) setError(message(error)) })
    return () => { controller.abort(); urls.forEach(URL.revokeObjectURL) }
  }, [])
  return <section><div className="page-heading"><h1>Tài liệu API</h1></div>{error && <p className="error" role="alert">{error}</p>}{html ? <iframe ref={frame} title="Swagger API" srcDoc={html} onLoad={authorize} className="api-docs-frame" /> : !error && <p className="loading">Đang tải tài liệu…</p>}</section>
}
