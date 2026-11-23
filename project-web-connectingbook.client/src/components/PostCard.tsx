import { useState } from 'react'
import { api, message } from '../api/client'
import type { Post } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { Avatar, ProtectedImage } from './ProtectedImage'
import { ReportDialog } from './ReportDialog'
import { Comments } from './Comments'
import { ReelPlayer } from './ReelPlayer'

const statusLabels: Record<string, string> = { Processing: 'Đang xử lý', Failed: 'Xử lý thất bại', Hidden: 'Đã ẩn', Visible: 'Đã đăng' }
export function PostCard({ post, onChanged }: { post: Post; onChanged: () => void }) {
  const { session } = useAuth()
  const mine = post.author.id === session!.user.id
  const visible = !post.status || post.status === 'Visible'
  const [comments, setComments] = useState(false)
  const [report, setReport] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(post.text)
  const [imageIds, setImageIds] = useState(post.media.filter(x => x.kind === 'Image').map(x => x.id))
  const [added, setAdded] = useState<File[]>([])
  async function action(kind: 'like' | 'delete' | 'edit') {
    setBusy(true); setError('')
    try {
      if (kind === 'delete') {
        if (!window.confirm('Xóa nội dung này? Báo cáo và lịch sử kiểm duyệt vẫn được giữ.')) return
        await api(`/api/posts/${post.id}`, { method: 'DELETE', headers: { 'If-Match': post.version } })
      } else if (kind === 'like') await api(`/api/posts/${post.id}/like`, { method: post.isLikedByMe ? 'DELETE' : 'PUT' })
      else {
        const ids = [...imageIds]
        for (const file of added) {
          const form = new FormData(); form.append('file', file); form.append('purpose', 'post')
          ids.push((await api<{ imageId: string }>('/api/media/images', { method: 'POST', body: form })).imageId)
        }
        const body = post.type === 'Reel' ? { caption: draft } : { text: draft, imageIds: ids }
        await api(`/api/posts/${post.id}`, { method: 'PATCH', headers: { 'If-Match': post.version }, body: JSON.stringify(body) })
        setEditing(false); setAdded([])
      }
      onChanged()
    } catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  function reorder(index: number, direction: number) {
    const next = [...imageIds]; const destination = index + direction
    if (destination < 0 || destination >= next.length) return
    ;[next[index], next[destination]] = [next[destination], next[index]]
    setImageIds(next)
  }
  return <article className="card post-card"><header className="post-header"><a href={`#profile/${post.author.id}`}><Avatar id={post.author.avatarMediaId} name={post.author.displayName} /></a><div className="post-author"><a href={`#profile/${post.author.id}`}><strong>{post.author.displayName}</strong></a><a className="post-time" href={`#post/${post.id}`}>{new Date(post.publishedAt ?? post.createdAt).toLocaleString('vi-VN')}{post.updatedAt && ' · đã chỉnh sửa'}</a></div>{mine ? <div className="inline-actions">{visible && <button className="text-button" onClick={() => setEditing(!editing)}>Sửa</button>}<button className="text-button danger" disabled={busy} onClick={() => action('delete')}>Xóa</button></div> : <button className="quiet" onClick={() => setReport(true)} aria-label={`Báo cáo bài của ${post.author.displayName}`}>Báo cáo</button>}</header>
    {post.status && post.status !== 'Visible' && <div className={`status-banner ${post.status === 'Hidden' || post.status === 'Failed' ? 'warning' : ''}`}><strong>{statusLabels[post.status] ?? post.status}</strong>{post.moderationReason && <p>{post.moderationReason}</p>}{post.failureCode && <p>Không xử lý được video. Bạn có thể tạo reel mới để thử lại.</p>}</div>}
    {editing ? <form className="edit-post" onSubmit={event => { event.preventDefault(); action('edit') }}><label>Nội dung<textarea value={draft} onChange={event => setDraft(event.target.value)} maxLength={post.type === 'Reel' ? 500 : 5000} rows={4} /></label>{post.type === 'Post' && <><ol className="edit-images">{imageIds.map((id, index) => <li key={id}><ProtectedImage id={id} alt={`Ảnh ${index + 1}`} /><div className="inline-actions"><button type="button" className="quiet" disabled={index === 0} onClick={() => reorder(index, -1)}>Lên</button><button type="button" className="quiet" disabled={index === imageIds.length - 1} onClick={() => reorder(index, 1)}>Xuống</button><button type="button" className="text-button danger" onClick={() => setImageIds(imageIds.filter(value => value !== id))}>Bỏ ảnh</button></div></li>)}</ol><label>Thêm ảnh<input type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={event => setAdded(Array.from(event.target.files ?? []))} /></label><small>{imageIds.length + added.length}/4 ảnh</small></>}<div className="inline-actions"><button className="primary" disabled={busy || imageIds.length + added.length > 4}>Lưu thay đổi</button><button type="button" className="quiet" onClick={() => setEditing(false)}>Hủy</button></div></form> : <><p className="post-text">{post.text}</p><div className={`post-media ${post.media.length > 1 ? 'media-grid' : ''}`}>{post.media.filter(x => x.kind === 'Image').map(image => <ProtectedImage key={image.id} id={image.id} alt={`Ảnh trong bài của ${post.author.displayName}`} />)}{post.media.filter(x => x.kind === 'Video' && visible).map(video => <ReelPlayer key={video.id} id={video.id} />)}</div></>}
    {visible && <><div className="post-counts"><span>{post.likeCount} lượt thích</span><span>{post.commentCount} bình luận</span></div><div className="post-actions"><button className={post.isLikedByMe ? 'liked' : ''} aria-pressed={post.isLikedByMe} disabled={busy} onClick={() => action('like')}>{post.isLikedByMe ? 'Đã thích' : 'Thích'}</button><button aria-expanded={comments} onClick={() => setComments(!comments)}>Bình luận</button></div>{comments && <Comments postId={post.id} />}</>}
    {error && <p role="alert" className="error">{error}</p>}{report && <ReportDialog postId={post.id} onClose={() => setReport(false)} />}
  </article>
}
