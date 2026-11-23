import { getSession, restoreSession } from './client'

let registration: Promise<void> | null = null
export function sendMediaToken() {
  navigator.serviceWorker?.controller?.postMessage({ type: 'auth', token: getSession()?.accessToken ?? null })
}
async function bindMediaToken() {
  const controller = navigator.serviceWorker.controller
  if (!controller) throw new Error('Trình phát video chưa sẵn sàng.')
  await new Promise<void>((resolve, reject) => {
    const channel = new MessageChannel()
    const timeout = setTimeout(() => { channel.port1.close(); reject(new Error('Không thể xác thực trình phát video.')) }, 3000)
    channel.port1.onmessage = () => { clearTimeout(timeout); channel.port1.close(); resolve() }
    controller.postMessage({ type: 'auth', token: getSession()?.accessToken ?? null }, [channel.port2])
  })
}
export function ensureMediaWorker(): Promise<void> {
  if (registration) return registration
  registration = (async () => {
    if (!('serviceWorker' in navigator) || !window.isSecureContext) throw new Error('Phát video cần kết nối HTTPS và trình duyệt hỗ trợ service worker.')
    await navigator.serviceWorker.register('/media-worker.js', { scope: '/' })
    await navigator.serviceWorker.ready
    if (!navigator.serviceWorker.controller) {
      await new Promise<void>((resolve, reject) => {
        const timeout = setTimeout(() => { navigator.serviceWorker.removeEventListener('controllerchange', ready); reject(new Error('Chưa thể khởi tạo trình phát video. Vui lòng tải lại trang.')) }, 10000)
        const ready = () => { clearTimeout(timeout); resolve() }
        navigator.serviceWorker.addEventListener('controllerchange', ready, { once: true })
      })
    }
    await bindMediaToken()
    navigator.serviceWorker.addEventListener('controllerchange', sendMediaToken)
    navigator.serviceWorker.addEventListener('message', event => {
      if (event.source !== navigator.serviceWorker.controller || event.data?.type !== 'media-auth-needed') return
      const current = getSession()
      const resume = () => { sendMediaToken(); window.dispatchEvent(new Event('connectingbook-media-ready')) }
      if (current && Date.parse(current.expiresAt) - Date.now() > 30000) resume()
      else restoreSession().then(value => { if (value) resume() }).catch(() => { /* The player presents a retry control. */ })
    })
  })().catch(error => { registration = null; throw error })
  return registration
}
