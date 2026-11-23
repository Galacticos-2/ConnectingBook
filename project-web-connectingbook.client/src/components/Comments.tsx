import { useEffect, useState } from 'react'
import { api, message } from '../api/client'
import type { Comment, Page } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { Avatar } from './ProtectedImage'
import type { FormEvent } from 'react'

export function Comments({ postId }: { postId: string }) {
  const { session } = useAuth()
  const [items, setItems] = useState<Comment[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function load(next?: string) {
    setBusy(true)
    try { const page = await api<Page<Comment>>(`/api/posts/${postId}/comments${next ? `?cursor=${encodeURIComponent(next)}` : ''}`); setItems(old => next ? [...old, ...page.items] : page.items); setCursor(page.nextCursor) }
    catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  useEffect(() => { let active = true; api<Page<Comment>>(`/api/posts/${postId}/comments`).then(page => { if (active) { setItems(page.items); setCursor(page.nextCursor) } }).catch(error => { if (active) setError(message(error)) }); return () => { active = false } }, [postId])
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { await api(`/api/posts/${postId}/comments`, { method: 'POST', body: JSON.stringify({ text }) }); setText(''); await load() }
    catch (error) { setError(message(error)); setBusy(false) }
  }
  async function change(comment: Comment, remove: boolean) {
    const text = remove ? null : window.prompt('Sửa bình luận', comment.text)
    if (!remove && text === null) return
    setError('')
    try { await api(`/api/comments/${comment.id}`, { method: remove ? 'DELETE' : 'PATCH', body: remove ? undefined : JSON.stringify({ text }) }); await load() }
    catch (error) { setError(message(error)) }
  }
  return <section className="comments"><h3>Bình luận</h3>{items.map(comment => <div className="comment" key={comment.id}><Avatar id={comment.author.avatarMediaId} name={comment.author.displayName} /><div><strong>{comment.author.displayName}</strong><p>{comment.text}</p><small className="muted">{new Date(comment.createdAt).toLocaleString('vi-VN')}{comment.editedAt && ' · đã chỉnh sửa'}</small>{comment.author.id === session!.user.id && <div className="inline-actions"><button className="text-button" onClick={() => change(comment, false)}>Sửa</button><button className="text-button danger" onClick={() => change(comment, true)}>Xóa</button></div>}</div></div>)}
    {cursor && <button className="quiet" disabled={busy} onClick={() => load(cursor)}>Xem thêm bình luận</button>}
    <form onSubmit={submit}><label className="sr-only" htmlFor={`comment-${postId}`}>Viết bình luận</label><input id={`comment-${postId}`} value={text} onChange={event => setText(event.target.value)} maxLength={1000} placeholder="Viết bình luận…" required /><button className="primary" disabled={busy || !text.trim()}>Gửi</button></form>{error && <p role="alert" className="error">{error}</p>}
  </section>
}
