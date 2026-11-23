export class ApiError extends Error {
  status: number
  code?: string
  constructor(status: number, message: string, code?: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

export interface SessionUser { id: string; displayName: string; role: 'Member' | 'Admin'; avatarMediaId: string | null }
export interface Session { accessToken: string; expiresAt: string; user: SessionUser }
export interface Page<T> { items: T[]; nextCursor: string | null }
export interface Author { id: string; displayName: string; avatarMediaId: string | null }
export interface Media { id: string; thumbnailId: string | null; kind: string; width: number; height: number }
export interface Post { id: string; author: Author; type: 'Post' | 'Reel'; text: string; media: Media[]; publishedAt: string | null; createdAt: string; updatedAt: string | null; likeCount: number; commentCount: number; isLikedByMe: boolean; version: string; status: string | null; moderationReason: string | null; failureCode: string | null }
export interface Profile { id: string; displayName: string; bio: string; avatarMediaId: string | null; followerCount: number; followingCount: number; isFollowedByMe: boolean }
export interface Comment { id: string; postId: string; author: Author; text: string; createdAt: string; editedAt: string | null }

let session: Session | null = null
let refreshPromise: Promise<Session | null> | null = null
const listeners = new Set<() => void>()
export const getSession = () => session
export function subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener) } }
export function setSession(value: Session | null) {
  session = value
  navigator.serviceWorker?.controller?.postMessage({ type: 'auth', token: value?.accessToken ?? null })
  listeners.forEach(listener => listener())
}

async function responseError(response: Response): Promise<ApiError> {
  const body = await response.json().catch(() => ({}))
  const fields = body.errors ? Object.values(body.errors as Record<string, string[]>).flat().join(' ') : ''
  return new ApiError(response.status, body.detail || fields || body.title || 'Không thể hoàn tất yêu cầu.', body.code)
}

export function restoreSession(): Promise<Session | null> {
  if (!refreshPromise) {
    refreshPromise = fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' })
      .then(async response => {
        if (response.status === 401) { setSession(null); return null }
        if (!response.ok) throw await responseError(response)
        const value = await response.json() as Session
        setSession(value)
        return value
      }).finally(() => { refreshPromise = null })
  }
  return refreshPromise
}

export async function api<T>(path: string, options: RequestInit = {}, authenticated = true): Promise<T> {
  if (authenticated && session && Date.parse(session.expiresAt) - Date.now() < 30_000) await restoreSession()
  const headers = new Headers(options.headers)
  if (authenticated && session) headers.set('Authorization', `Bearer ${session.accessToken}`)
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  let response = await fetch(path, { ...options, headers, credentials: 'same-origin' })
  if (response.status === 401 && authenticated && session) {
    const renewed = await restoreSession()
    if (renewed) {
      headers.set('Authorization', `Bearer ${renewed.accessToken}`)
      response = await fetch(path, { ...options, headers, credentials: 'same-origin' })
    }
  }
  if (!response.ok) throw await responseError(response)
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export async function mediaBlob(id: string, signal: AbortSignal, path = `/api/media/${id}`): Promise<string> {
  if (session && Date.parse(session.expiresAt) - Date.now() < 30_000) await restoreSession()
  const response = await fetch(path, { signal, headers: session ? { Authorization: `Bearer ${session.accessToken}` } : {} })
  if (!response.ok) throw await responseError(response)
  return URL.createObjectURL(await response.blob())
}

export async function logout() {
  try { await api('/api/auth/logout', { method: 'POST' }, false) }
  finally { setSession(null) }
}

export const message = (error: unknown) => error instanceof Error ? error.message : 'Có lỗi xảy ra. Vui lòng thử lại.'
