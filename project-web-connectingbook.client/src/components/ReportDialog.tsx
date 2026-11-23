import { useState } from 'react'
import { api, message } from '../api/client'
import type { FormEvent } from 'react'

export function ReportDialog({ postId, onClose }: { postId: string; onClose: () => void }) {
  const [reason, setReason] = useState('Spam')
  const [description, setDescription] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { await api(`/api/posts/${postId}/reports`, { method: 'POST', body: JSON.stringify({ reason, description }) }); setSuccess(true) }
    catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  return <div className="modal-backdrop"><section className="modal card" role="dialog" aria-modal="true" aria-labelledby="report-title"><div className="section-heading"><h2 id="report-title">Báo cáo nội dung</h2><button type="button" className="quiet" onClick={onClose} aria-label="Đóng hộp thoại">Đóng</button></div>
    {success ? <><p role="status">Đã gửi báo cáo. Bạn có thể theo dõi kết quả ở mục Báo cáo của tôi.</p><button className="primary" onClick={onClose}>Hoàn tất</button></> : <form onSubmit={submit}><p className="muted">Danh tính của bạn không được chia sẻ với tác giả.</p><label>Lý do<select value={reason} onChange={event => setReason(event.target.value)}><option value="Spam">Spam</option><option value="Harassment">Quấy rối</option><option value="Inappropriate">Nội dung không phù hợp</option><option value="Other">Lý do khác</option></select></label><label>Mô tả<textarea required={reason === 'Other'} value={description} onChange={event => setDescription(event.target.value)} maxLength={500} rows={4} /></label>{error && <p role="alert" className="error">{error}</p>}<button className="primary full" disabled={busy || (reason === 'Other' && !description.trim())}>{busy ? 'Đang gửi…' : 'Gửi báo cáo'}</button></form>}
  </section></div>
}
