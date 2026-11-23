import { useEffect, useState } from 'react'
import { api, message } from '../api/client'
import type { Post } from '../api/client'
import { PostCard } from '../components/PostCard'

export function PostPage({ id }: { id: string }) {
  const [post, setPost] = useState<Post | null>(null)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => { const controller = new AbortController(); api<Post>(`/api/posts/${id}`, { signal: controller.signal }).then(setPost).catch(error => { if (!controller.signal.aborted) setError(message(error)) }); return () => controller.abort() }, [id, revision])
  return <section><div className="page-heading"><h1>Chi tiết bài viết</h1></div>{error ? <p className="card error" role="alert">{error}</p> : post ? <PostCard post={post} onChanged={() => setRevision(value => value + 1)} /> : <p className="loading">Đang tải bài viết…</p>}</section>
}
