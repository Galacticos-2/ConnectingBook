import { useEffect, useState } from 'react'
import { api, message } from '../api/client'
import type { Page } from '../api/client'

interface Report { id: string; postId: string; reason: string; description: string | null; status: string; createdAt: string }
const labels: Record<string, string> = { Pending: 'Đang chờ xử lý', ClosedKept: 'Đã xử lý · Giữ nguyên', ClosedHidden: 'Đã xử lý · Đã ẩn', ClosedAuthorDeleted: 'Tác giả đã xóa nội dung' }
const reasons: Record<string, string> = { Spam: 'Spam', Harassment: 'Quấy rối', Inappropriate: 'Nội dung không phù hợp', Other: 'Khác' }
export function ReportsPage() {
  const [items, setItems] = useState<Report[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { let active = true; api<Page<Report>>('/api/me/reports').then(page => { if (active) { setItems(page.items); setCursor(page.nextCursor) } }).catch(error => { if (active) setError(message(error)) }); return () => { active = false } }, [])
  async function more() { setBusy(true); try { const page = await api<Page<Report>>(`/api/me/reports?cursor=${encodeURIComponent(cursor!)}`); setItems(old => [...old, ...page.items]); setCursor(page.nextCursor) } catch (error) { setError(message(error)) } finally { setBusy(false) } }
  return <section><div className="page-heading"><div><span className="eyebrow">AN TOÀN CỘNG ĐỒNG</span><h1>Báo cáo của tôi</h1><p className="muted">Theo dõi kết quả những báo cáo bạn đã gửi.</p></div></div>{error && <p role="alert" className="error">{error}</p>}{items.length === 0 && !error && <div className="card empty-state"><h2>Chưa có báo cáo</h2><p>Các báo cáo bạn gửi sẽ xuất hiện tại đây.</p></div>}{items.map(report => <article className="card report-card" key={report.id}><span className="badge">{labels[report.status] ?? report.status}</span><h3>Báo cáo nội dung #{report.postId}</h3><p>{reasons[report.reason] ?? report.reason}{report.description && ` · ${report.description}`}</p><small className="muted">{new Date(report.createdAt).toLocaleString('vi-VN')}</small></article>)}{cursor && <button className="quiet" onClick={more} disabled={busy}>Xem thêm</button>}</section>
}
