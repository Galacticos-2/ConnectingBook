import { beforeEach, describe, expect, it, vi } from 'vitest'
import { StrictMode } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FeedPage } from '../src/pages/FeedPage'
import { api } from '../src/api/client'
import type { Post } from '../src/api/client'

vi.mock('../src/api/client', async () => ({ ...await vi.importActual('../src/api/client'), api: vi.fn() }))
vi.mock('../src/auth/useAuth', () => ({ useAuth: () => ({ session: { user: { id: 'me', displayName: 'Minh', avatarMediaId: null, role: 'Member' } }, loading: false, error: '' }) }))
const post = (id: string): Post => ({ id, author: { id: 'other', displayName: 'An', avatarMediaId: null }, type: 'Post', text: `Câu chuyện ${id}`, media: [], publishedAt: '2026-11-22T00:00:00Z', createdAt: '2026-11-22T00:00:00Z', updatedAt: null, likeCount: 0, commentCount: 0, isLikedByMe: false, version: id, status: null, moderationReason: null, failureCode: null })

describe('FeedPage', () => {
  beforeEach(() => vi.mocked(api).mockReset())
  it('sends only the live feed request during the development StrictMode replay', async () => {
    vi.mocked(api).mockResolvedValue({ items: [], nextCursor: null })
    render(<StrictMode><FeedPage composer={false} /></StrictMode>)
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument())
    expect(api).toHaveBeenCalledTimes(1)
    const options = vi.mocked(api).mock.calls[0][1]
    expect(options?.signal?.aborted).toBe(false)
    expect(screen.getByRole('button', { name: 'Làm mới' })).toBeEnabled()
  })
  it('does not start a request if the feed unmounts before its scheduled load', async () => {
    vi.mocked(api).mockResolvedValue({ items: [], nextCursor: null })
    const view = render(<FeedPage composer={false} />)
    view.unmount()
    await Promise.resolve()
    expect(api).not.toHaveBeenCalled()
  })
  it('loads more with cursor, avoids duplicate cards and refreshes from the first page', async () => {
    vi.mocked(api).mockResolvedValueOnce({ items: [post('1')], nextCursor: 'next-page' }).mockResolvedValueOnce({ items: [post('1'), post('2')], nextCursor: null }).mockResolvedValueOnce({ items: [post('3')], nextCursor: null })
    const user = userEvent.setup()
    render(<FeedPage composer={false} />)
    expect(await screen.findByText('Câu chuyện 1')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Xem thêm' }))
    expect(await screen.findByText('Câu chuyện 2')).toBeInTheDocument()
    expect(screen.getAllByText('Câu chuyện 1')).toHaveLength(1)
    expect(vi.mocked(api).mock.calls[1][0]).toBe('/api/feed?cursor=next-page')
    await user.click(screen.getByRole('button', { name: 'Làm mới' }))
    expect(await screen.findByText('Câu chuyện 3')).toBeInTheDocument()
    expect(screen.queryByText('Câu chuyện 1')).not.toBeInTheDocument()
  })
  it('shows an error and permits retry without pretending the feed is empty', async () => {
    vi.mocked(api).mockRejectedValueOnce(new Error('Mất kết nối')).mockResolvedValueOnce({ items: [], nextCursor: null })
    const user = userEvent.setup()
    render(<FeedPage composer={false} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Mất kết nối')
    await user.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
    expect(screen.getByText('Câu chuyện đầu tiên bắt đầu từ bạn')).toBeInTheDocument()
  })
})
