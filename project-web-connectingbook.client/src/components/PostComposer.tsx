import { useState } from 'react'
import { api, message } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { Avatar } from './ProtectedImage'
import type { FormEvent } from 'react'

export function PostComposer({ onCreated }: { onCreated: () => void }) {
  const { session } = useAuth()
  const [text, setText] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [fileKey, setFileKey] = useState(0)
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      if (files.length > 4) throw new Error('Mỗi bài tối đa 4 ảnh.')
      const imageIds: string[] = []
      for (const file of files) {
        const data = new FormData(); data.append('file', file); data.append('purpose', 'post')
        imageIds.push((await api<{ imageId: string }>('/api/media/images', { method: 'POST', body: data })).imageId)
      }
      await api('/api/posts', { method: 'POST', body: JSON.stringify({ text, imageIds }) })
      setText(''); setFiles([]); setFileKey(key => key + 1); onCreated()
    } catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  return <form className="card composer" onSubmit={submit}><div className="composer-top"><Avatar id={session!.user.avatarMediaId} name={session!.user.displayName} /><label className="sr-only" htmlFor="post-text">Nội dung bài viết</label><textarea id="post-text" placeholder={`${session!.user.displayName}, bạn muốn chia sẻ điều gì?`} value={text} onChange={event => setText(event.target.value)} maxLength={5000} rows={3} /></div>
    {files.length > 0 && <ol className="file-list">{files.map((file, index) => <li key={`${file.name}-${index}`}>{file.name} <button type="button" className="text-button" onClick={() => setFiles(files.filter((_, i) => i !== index))}>Bỏ ảnh</button></li>)}</ol>}
    {error && <p role="alert" className="error">{error}</p>}
    <div className="composer-actions"><label className="upload-button">Ảnh <span className="muted">· Tối đa 4 ảnh</span><input key={fileKey} type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={event => { const selected = Array.from(event.target.files ?? []); setFiles(selected); setError(selected.length > 4 ? 'Mỗi bài tối đa 4 ảnh.' : '') }} /></label><button className="primary" disabled={busy || (!text.trim() && !files.length) || files.length > 4}>{busy ? 'Đang đăng…' : 'Đăng bài'}</button></div>
  </form>
}
