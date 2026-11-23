import { useState } from 'react'
import { api, message } from '../api/client'
import { FeedPage } from './FeedPage'
import type { FormEvent } from 'react'

export function ReelsPage() {
  const [revision, setRevision] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState(false)
  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget; const body = new FormData(form)
    const file = body.get('file') as File
    if (file.size > 50 * 1024 * 1024) { setError('Video tối đa 50 MB.'); return }
    setBusy(true); setError(''); setSuccess(false)
    try { await api('/api/reels', { method: 'POST', body }); form.reset(); setSuccess(true); setRevision(value => value + 1) }
    catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  return <><section className="card reel-upload"><h2>Chia sẻ một khoảnh khắc</h2><p className="muted">Video MP4 tối đa 60 giây, 50 MB. Reel xuất hiện sau khi xử lý thành công.</p><form onSubmit={upload}><label>Video<input name="file" type="file" accept="video/mp4" required /></label><label>Mô tả<textarea name="caption" maxLength={500} rows={2} /></label>{error && <p role="alert" className="error">{error}</p>}{success && <p role="status" className="success">Đã tiếp nhận video. Xem trạng thái tại <a href="#my-posts">Nội dung của tôi</a>.</p>}<button className="primary" disabled={busy}>{busy ? 'Đang tải video…' : 'Đăng reel'}</button></form></section><FeedPage key={revision} path="/api/reels" title="Reels" description="Những khoảnh khắc mới nhất từ cộng đồng." composer={false} /></>
}
