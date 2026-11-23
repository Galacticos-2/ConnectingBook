import { useState } from 'react'
import { api, message, setSession } from '../api/client'
import type { FormEvent } from 'react'
import type { Session } from '../api/client'

export function AuthPage() {
  const [register, setRegister] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const fields = new FormData(event.currentTarget)
    const email = String(fields.get('email')).trim()
    const password = String(fields.get('password'))
    setBusy(true); setError('')
    try {
      if (register) await api('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password, displayName: String(fields.get('displayName')).trim() }) }, false)
      setSession(await api<Session>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }, false))
    } catch (error) { setError(message(error)) }
    finally { setBusy(false) }
  }
  return <main className="auth-layout">
    <section className="auth-story"><div className="wordmark"><span className="brand-symbol">cb</span> Connectbook</div><span className="eyebrow">GẮN KẾT TỪ NHỮNG ĐIỀU NHỎ</span><h1>Mỗi câu chuyện.<br />Một kết nối mới.</h1><p>Một nơi để chia sẻ khoảnh khắc, theo dõi người bạn quan tâm và cùng trò chuyện.</p><div className="auth-mosaic"><div className="mosaic-a"><span>Chia sẻ</span><strong>Điều bạn muốn kể.</strong></div><div className="mosaic-b"><span>Kết nối</span><strong>Với những người bạn quan tâm.</strong></div></div><small>Nội dung cộng đồng chỉ dành cho thành viên đã đăng nhập.</small></section>
    <section className="auth-panel"><form onSubmit={submit}><span className="eyebrow">CHÀO MỪNG BẠN</span><h2>{register ? 'Bắt đầu câu chuyện của bạn' : 'Rất vui được gặp lại'}</h2><p className="muted">{register ? 'Tạo tài khoản để tham gia cộng đồng.' : 'Đăng nhập để tiếp tục kết nối.'}</p>
      {register && <label>Tên hiển thị<input name="displayName" autoComplete="nickname" required minLength={2} maxLength={50} /></label>}
      <label>Email<input name="email" type="email" autoComplete="email" required maxLength={254} /></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'} required minLength={register ? 10 : 1} maxLength={128} /></label>
      {register && <p className="form-hint">Mật khẩu từ 10 đến 128 ký tự.</p>}
      {error && <p role="alert" className="error">{error}</p>}
      <button className="primary full" disabled={busy}>{busy ? 'Đang xử lý…' : register ? 'Tạo tài khoản' : 'Đăng nhập'}</button>
      <p className="auth-switch">{register ? 'Bạn đã có tài khoản?' : 'Bạn chưa có tài khoản?'} <button type="button" className="text-button" onClick={() => { setRegister(!register); setError('') }}>{register ? 'Đăng nhập' : 'Đăng ký ngay'}</button></p>
    </form></section>
  </main>
}
