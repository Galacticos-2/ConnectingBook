import { useEffect, useState } from 'react'
import { AuthProvider } from './auth/AuthProvider'
import { useAuth } from './auth/useAuth'
import { AuthPage } from './pages/AuthPage'
import { FeedPage } from './pages/FeedPage'
import { ProfilePage } from './pages/ProfilePage'
import { MyPostsPage } from './pages/MyPostsPage'
import { ReportsPage } from './pages/ReportsPage'
import { ModerationPage } from './pages/ModerationPage'
import { ReelsPage } from './pages/ReelsPage'
import { PostPage } from './pages/PostPage'
import { ApiDocsPage } from './pages/ApiDocsPage'
import { Avatar } from './components/ProtectedImage'
import { logout, message } from './api/client'
import './App.css'

function Workspace() {
  const { session, loading, error: sessionError } = useAuth()
  const [route, setRoute] = useState(window.location.hash.slice(1) || 'feed')
  const [error, setError] = useState('')
  useEffect(() => { const change = () => { setRoute(window.location.hash.slice(1) || 'feed'); window.scrollTo(0, 0) }; window.addEventListener('hashchange', change); return () => window.removeEventListener('hashchange', change) }, [])
  if (loading) return <main className="startup"><span className="brand-symbol">cb</span><p>Đang mở Connectbook…</p></main>
  if (!session) return <><AuthPage />{sessionError && <p role="alert" className="startup-error">{sessionError}</p>}</>
  const user = session.user
  const [view, id] = route.split('/')
  const links = [{ href: 'feed', label: 'Bảng tin', icon: '▤' }, { href: 'reels', label: 'Reels', icon: '▷' }, { href: `profile/${user.id}`, label: 'Hồ sơ của tôi', icon: '◉' }, { href: 'my-posts', label: 'Nội dung của tôi', icon: '▧' }, { href: 'reports', label: 'Báo cáo của tôi', icon: '⚑' }]
  let content
  if (view === 'profile' && id) content = <ProfilePage key={id} id={id} />
  else if (view === 'post' && id) content = <PostPage key={id} id={id} />
  else if (view === 'my-posts') content = <MyPostsPage />
  else if (view === 'reports') content = <ReportsPage />
  else if (view === 'reels') content = <ReelsPage />
  else if (view === 'docs') content = <ApiDocsPage />
  else if (view === 'moderation' && user.role === 'Admin') content = <ModerationPage />
  else content = <FeedPage />
  return <><header className="topbar"><a className="wordmark" href="#feed"><span className="brand-symbol">cb</span>Connectbook</a><span className="topbar-note">Những câu chuyện đưa ta gần nhau hơn.</span><a href={`#profile/${user.id}`} className="topbar-user"><Avatar id={user.avatarMediaId} name={user.displayName} /><span>{user.displayName}</span></a></header>
    <div className={`app-layout ${view === 'moderation' ? 'wide-layout' : ''}`}><aside className="sidebar"><nav aria-label="Điều hướng chính">{links.map(link => <a className={route === link.href ? 'active' : ''} key={link.href} href={`#${link.href}`}><span aria-hidden="true">{link.icon}</span>{link.label}</a>)}{user.role === 'Admin' && <a href="#moderation" className={view === 'moderation' ? 'active' : ''}><span aria-hidden="true">◇</span>Kiểm duyệt</a>}</nav><div className="sidebar-bottom"><p>Chào, <strong>{user.displayName}</strong></p><button className="text-button" onClick={() => logout().catch(error => setError(message(error)))}>Đăng xuất</button><small>Connectbook · Cùng chia sẻ, cùng kết nối.</small></div></aside>
      <main className="main-content" key={route}>{error && <p className="error" role="alert">{error}</p>}{content}</main>
      {view !== 'moderation' && <aside className="right-rail"><section className="card community-note"><span className="eyebrow">GÓC CỘNG ĐỒNG</span><div className="connection-art" aria-hidden="true"><span>c</span><span>b</span></div><h2>Kết nối bắt đầu từ một câu chuyện.</h2><p>Ghé hồ sơ của người bạn quan tâm và chọn Theo dõi để thấy bài viết của họ trên bảng tin.</p><a className="quiet link-button" href={`#profile/${user.id}`}>Hoàn thiện hồ sơ</a></section><section className="rail-guidelines"><h3>Một cộng đồng tử tế</h3><p>Tôn trọng nhau. Bảo vệ thông tin cá nhân. Báo cáo nội dung không phù hợp.</p><small>Giao diện tiếng Việt · Nội dung dành cho thành viên.</small></section></aside>}
    </div></>
}

export default function App() { return <AuthProvider><Workspace /></AuthProvider> }
