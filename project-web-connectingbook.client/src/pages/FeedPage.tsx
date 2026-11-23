import { useEffect, useState } from 'react'
import { api, message } from '../api/client'
import type { Page, Post } from '../api/client'
import { PostComposer } from '../components/PostComposer'
import { PostCard } from '../components/PostCard'

export function FeedPage({ path = '/api/feed', title = 'Bảng tin', description = 'Những câu chuyện từ bạn và người bạn theo dõi.', composer = true }: { path?: string; title?: string; description?: string; composer?: boolean }) {
  const [items, setItems] = useState<Post[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    // StrictMode replays setup/cleanup before this microtask. Do not send the discarded request.
    queueMicrotask(() => {
      if (controller.signal.aborted) return
      api<Page<Post>>(path, { signal: controller.signal }).then(page => {
        if (controller.signal.aborted) return
        setItems(page.items); setCursor(page.nextCursor); setError('')
      }).catch(error => { if (!controller.signal.aborted) setError(message(error)) }).finally(() => { if (!controller.signal.aborted) setBusy(false) })
    })
    return () => controller.abort()
  }, [path, revision])
  function reload() { setBusy(true); setRevision(value => value + 1) }
  async function more() {
    if (!cursor) return
    setBusy(true); setError('')
    try { const page = await api<Page<Post>>(`${path}${path.includes('?') ? '&' : '?'}cursor=${encodeURIComponent(cursor)}`); setItems(old => { const ids = new Set(old.map(x => x.id)); return [...old, ...page.items.filter(x => !ids.has(x.id))] }); setCursor(page.nextCursor) }
    catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  return <div className="feed"><div className="page-heading"><div><span className="eyebrow">CỘNG ĐỒNG CỦA BẠN</span><h1>{title}</h1><p className="muted">{description}</p></div><button className="quiet" disabled={busy} onClick={reload}>Làm mới</button></div>{composer && <PostComposer onCreated={reload} />}{error && <div role="alert" className="card error">{error}<button className="quiet" onClick={reload}>Thử lại</button></div>}{items.map(post => <PostCard key={`${post.id}-${post.version}`} post={post} onChanged={reload} />)}{busy && <p role="status" className="loading">Đang tải nội dung…</p>}{!busy && !error && items.length === 0 && <section className="card empty-state"><span className="empty-symbol">cb</span><h2>Câu chuyện đầu tiên bắt đầu từ bạn</h2><p>Đăng một bài viết hoặc ghé hồ sơ người bạn quan tâm để theo dõi họ.</p></section>}{cursor && <button className="quiet load-more" disabled={busy} onClick={more}>Xem thêm</button>}</div>
}
