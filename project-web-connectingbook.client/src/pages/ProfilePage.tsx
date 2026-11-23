import { useEffect, useState } from 'react'
import { api, message, setSession } from '../api/client'
import type { Profile } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { Avatar } from '../components/ProtectedImage'
import { FeedPage } from './FeedPage'
import type { FormEvent } from 'react'

export function ProfilePage({ id }: { id: string }) {
  const { session } = useAuth()
  const [profile, setProfile] = useState<Profile | null>(null)
  const [editing, setEditing] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const mine = session!.user.id === id
  useEffect(() => { const controller = new AbortController(); api<Profile>(`/api/users/${id}`, { signal: controller.signal }).then(setProfile).catch(error => { if (!controller.signal.aborted) setError(message(error)) }); return () => controller.abort() }, [id])
  async function follow() {
    setBusy(true); setError('')
    try { await api(`/api/users/${id}/follow`, { method: profile!.isFollowedByMe ? 'DELETE' : 'PUT' }); setProfile(await api<Profile>(`/api/users/${id}`)) }
    catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('')
    const fields = new FormData(event.currentTarget)
    try {
      const file = fields.get('avatar') as File | null
      let avatarMediaId = profile!.avatarMediaId
      if (file && file.size) { const upload = new FormData(); upload.append('file', file); upload.append('purpose', 'avatar'); avatarMediaId = (await api<{ imageId: string }>('/api/media/images', { method: 'POST', body: upload })).imageId }
      const next = await api<Profile>('/api/me/profile', { method: 'PATCH', body: JSON.stringify({ displayName: fields.get('displayName'), bio: fields.get('bio'), avatarMediaId }) })
      setProfile(next); setEditing(false); setSession({ ...session!, user: { ...session!.user, displayName: next.displayName, avatarMediaId: next.avatarMediaId } })
    } catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  return <div>{error && <p role="alert" className="error">{error}</p>}{!profile ? <p className="loading">{error ? 'Không thể mở hồ sơ.' : 'Đang tải hồ sơ…'}</p> : <><section className="card profile-card"><div className="profile-cover" /><div className="profile-content"><Avatar id={profile.avatarMediaId} name={profile.displayName} /><div className="section-heading"><div><h1>{profile.displayName}</h1><p className="bio">{profile.bio || 'Chưa có tiểu sử.'}</p></div>{mine ? <button className="quiet" onClick={() => setEditing(!editing)}>Sửa hồ sơ</button> : <button className={profile.isFollowedByMe ? 'quiet' : 'primary'} disabled={busy} onClick={follow}>{profile.isFollowedByMe ? 'Bỏ theo dõi' : 'Theo dõi'}</button>}</div><div className="profile-stats"><span><strong>{profile.followerCount}</strong> người theo dõi</span><span><strong>{profile.followingCount}</strong> đang theo dõi</span></div>{editing && <form onSubmit={save} className="profile-form"><label>Tên hiển thị<input name="displayName" defaultValue={profile.displayName} required minLength={2} maxLength={50} /></label><label>Tiểu sử<textarea name="bio" defaultValue={profile.bio} maxLength={160} /></label><label>Ảnh đại diện · tối đa 2 MB<input name="avatar" type="file" accept="image/jpeg,image/png,image/webp" /></label><div className="inline-actions"><button className="primary" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button><button className="quiet" type="button" onClick={() => setEditing(false)}>Hủy</button></div></form>}</div></section><FeedPage key={id} path={`/api/users/${id}/posts`} title="Câu chuyện đã chia sẻ" description="Bài viết và reel đang hiển thị." composer={false} /></>}</div>
}
