import { useEffect, useRef, useState } from 'react'
import { ensureMediaWorker, sendMediaToken } from '../api/media'
import { message } from '../api/client'

export function ReelPlayer({ id, path }: { id: string; path?: string }) {
  const [ready, setReady] = useState(false)
  const [error, setError] = useState('')
  const player = useRef<HTMLVideoElement>(null)
  useEffect(() => {
    let active = true
    ensureMediaWorker().then(() => { if (active) setReady(true) }).catch(error => { if (active) setError(message(error)) })
    const retry = () => {
      const video = player.current
      if (!video) return
      const time = video.currentTime; const playing = !video.paused
      sendMediaToken(); video.load()
      video.addEventListener('loadedmetadata', () => { video.currentTime = time; if (playing) video.play().catch(() => {}) }, { once: true })
    }
    window.addEventListener('connectingbook-media-ready', retry)
    return () => { active = false; window.removeEventListener('connectingbook-media-ready', retry) }
  }, [id])
  return <div className="video-player">{ready && <video ref={player} src={path ?? `/api/media/${id}`} controls playsInline preload="metadata" onError={() => setError('Không tải được video. Nội dung có thể đã bị ẩn hoặc phiên đã hết hạn.')} />}{error && <div className="error" role="alert">{error} <button className="quiet" onClick={() => { setError(''); ensureMediaWorker().then(() => { setReady(true); sendMediaToken(); player.current?.load() }).catch(error => setError(message(error))) }}>Thử lại</button></div>}{!ready && !error && <p className="loading">Đang khởi tạo video…</p>}</div>
}
