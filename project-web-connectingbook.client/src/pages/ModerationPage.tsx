import { useEffect, useState } from 'react'
import { api, message } from '../api/client'
import type { Page, Post } from '../api/client'
import { ProtectedImage } from '../components/ProtectedImage'
import { ReelPlayer } from '../components/ReelPlayer'

interface Group { postId: string; oldestPendingAt: string; pendingCount: number }
interface Report { id: string; reason: string; description: string | null; status: string; createdAt: string; snapshotText: string; snapshotMediaIds: string[]; snapshotMedia: { id: string; kind: string }[] }
interface Detail { postId: string; groupVersion: string; currentPost: Post; reports: Report[] }
interface Action { id: string; actorId: string | null; decision: string; reason: string; createdAt: string }
const decisions: Record<string, string> = { Keep: 'Giữ nguyên', Hide: 'Ẩn', Restore: 'Khôi phục', AuthorDeleted: 'Tác giả đã xóa' }
export function ModerationPage() {
  const [groups, setGroups] = useState<Group[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [detail, setDetail] = useState<Detail | null>(null)
  const [history, setHistory] = useState<Action[]>([])
  const [historyCursor, setHistoryCursor] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const [lookup, setLookup] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { let active = true; api<Page<Group>>('/api/admin/report-groups').then(page => { if (active) { setGroups(page.items); setCursor(page.nextCursor) } }).catch(error => { if (active) setError(message(error)) }); return () => { active = false } }, [])
  async function loadQueue(next?: string) { const page = await api<Page<Group>>(`/api/admin/report-groups${next ? `?cursor=${encodeURIComponent(next)}` : ''}`); setGroups(old => next ? [...old, ...page.items] : page.items); setCursor(page.nextCursor) }
  async function open(id: string) {
    setBusy(true); setError('')
    try { setDetail(await api<Detail>(`/api/admin/report-groups/${id}`)); const page = await api<Page<Action>>(`/api/admin/posts/${id}/moderation-history`); setHistory(page.items); setHistoryCursor(page.nextCursor); setReason('') }
    catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  async function decide(decision: 'keep' | 'hide' | 'restore') {
    if (!detail) return
    setBusy(true); setError('')
    try {
      if (decision === 'restore') await api(`/api/admin/posts/${detail.postId}/restore`, { method: 'POST', headers: { 'If-Match': detail.currentPost.version }, body: JSON.stringify({ reason }) })
      else await api(`/api/admin/report-groups/${detail.postId}/decision`, { method: 'POST', body: JSON.stringify({ decision, reason, groupVersion: detail.groupVersion }) })
      await loadQueue(); await open(detail.postId)
    } catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  async function moreHistory() { if (!detail || !historyCursor) return; try { const page = await api<Page<Action>>(`/api/admin/posts/${detail.postId}/moderation-history?cursor=${encodeURIComponent(historyCursor)}`); setHistory(old => [...old, ...page.items]); setHistoryCursor(page.nextCursor) } catch (error) { setError(message(error)) } }
  return <section><div className="page-heading"><div><span className="eyebrow">KHÔNG GIAN QUẢN TRỊ</span><h1>Kiểm duyệt cộng đồng</h1><p className="muted">Xem ngữ cảnh, đưa ra quyết định và lưu lại lý do.</p></div><button className="quiet" disabled={busy} onClick={() => loadQueue().catch(error => setError(message(error)))}>Tải lại hàng đợi</button></div>{error && <p className="error" role="alert">{error} <button className="quiet" onClick={() => detail && open(detail.postId)}>Tải lại nội dung</button></p>}
    <form className="card lookup-form" onSubmit={event => { event.preventDefault(); open(lookup) }}><label htmlFor="moderation-id">Tra cứu nội dung đã được báo cáo theo ID</label><div className="inline-actions"><input id="moderation-id" inputMode="numeric" pattern="[0-9]+" value={lookup} onChange={event => setLookup(event.target.value)} required placeholder="ID nội dung" /><button className="quiet" disabled={busy}>Tra cứu</button></div></form>
    <div className="moderation-layout"><aside className="card queue"><h2>Đang chờ xử lý</h2>{groups.length === 0 && <p className="muted">Không có báo cáo đang chờ.</p>}{groups.map(group => <button className={`queue-item ${detail?.postId === group.postId ? 'selected' : ''}`} key={group.postId} onClick={() => open(group.postId)} disabled={busy}><strong>Nội dung #{group.postId}</strong><span>{group.pendingCount} báo cáo</span><small>{new Date(group.oldestPendingAt).toLocaleString('vi-VN')}</small></button>)}{cursor && <button className="quiet" onClick={() => loadQueue(cursor).catch(error => setError(message(error)))}>Xem thêm nhóm</button>}</aside>
      {detail ? <div className="moderation-detail"><section className="card"><div className="section-heading"><h2>Nội dung #{detail.postId}</h2><span className="badge">{detail.currentPost.status === 'Hidden' ? 'Đã ẩn' : detail.currentPost.status === 'Deleted' ? 'Đã xóa' : 'Đang hiển thị'}</span></div><h3>Phiên bản hiện tại</h3><p className="post-text">{detail.currentPost.text}</p><div className="post-media media-grid">{detail.currentPost.media.filter(x => x.kind === 'Image').map(image => <ProtectedImage key={image.id} id={image.id} alt="Ảnh hiện tại của bài viết" />)}</div><h3>Bản chụp khi báo cáo</h3>{detail.reports.map(report => <article className="snapshot" key={report.id}><p className="muted">Báo cáo #{report.id} · {new Date(report.createdAt).toLocaleString('vi-VN')}</p><p>{({ Spam: 'Spam', Harassment: 'Quấy rối', Inappropriate: 'Nội dung không phù hợp', Other: 'Khác' } as Record<string, string>)[report.reason] ?? report.reason}{report.description && ` · ${report.description}`}</p><p className="post-text">{report.snapshotText}</p><div className="post-media media-grid">{report.snapshotMediaIds.map(id => report.snapshotMedia.some(media => media.id === id && media.kind === 'Video') ? <ReelPlayer key={id} id={id} path={`/api/admin/reports/${report.id}/media/${id}`} /> : <ProtectedImage key={id} id={id} path={`/api/admin/reports/${report.id}/media/${id}`} alt="Tệp bằng chứng tại thời điểm báo cáo" />)}</div></article>)}
        {(detail.currentPost.status === 'Hidden' || detail.reports.some(x => x.status === 'Pending')) && <div className="decision-form"><label>Lý do quyết định<textarea value={reason} onChange={event => setReason(event.target.value)} maxLength={1000} rows={3} required /></label><div className="inline-actions">{detail.currentPost.status === 'Hidden' ? <button className="primary" disabled={busy || !reason.trim()} onClick={() => decide('restore')}>Khôi phục nội dung</button> : <><button className="quiet" disabled={busy || !reason.trim()} onClick={() => decide('keep')}>Giữ nguyên</button><button className="primary danger-button" disabled={busy || !reason.trim()} onClick={() => decide('hide')}>Ẩn nội dung</button></>}</div></div>}</section><section className="card"><h2>Lịch sử xử lý</h2>{history.length === 0 && <p className="muted">Chưa có quyết định.</p>}{history.map(action => <article className="history-item" key={action.id}><strong>{decisions[action.decision] ?? action.decision}</strong><p>{action.reason}</p><small className="muted">{action.actorId ? 'Quản trị viên' : 'Hệ thống'} · {new Date(action.createdAt).toLocaleString('vi-VN')}</small></article>)}{historyCursor && <button className="quiet" onClick={moreHistory}>Xem thêm lịch sử</button>}</section></div> : <div className="card empty-state"><h2>Chọn một nhóm báo cáo</h2><p>Đọc bản chụp và nội dung hiện tại trước khi quyết định.</p></div>}
    </div></section>
}
