import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ReportDialog } from '../src/components/ReportDialog'
import { api } from '../src/api/client'

vi.mock('../src/api/client', async () => ({ ...await vi.importActual('../src/api/client'), api: vi.fn() }))
describe('ReportDialog', () => {
  beforeEach(() => vi.mocked(api).mockReset())
  it('requires explanation for Other, then displays confirmation after submission', async () => {
    vi.mocked(api).mockResolvedValue({ id: '1', status: 'Pending' })
    const user = userEvent.setup()
    render(<ReportDialog postId="42" onClose={vi.fn()} />)
    await user.selectOptions(screen.getByLabelText('Lý do'), 'Other')
    expect(screen.getByRole('button', { name: 'Gửi báo cáo' })).toBeDisabled()
    await user.type(screen.getByLabelText('Mô tả'), 'Nội dung cần được xem xét')
    await user.click(screen.getByRole('button', { name: 'Gửi báo cáo' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Đã gửi báo cáo')
    expect(api).toHaveBeenCalledWith('/api/posts/42/reports', expect.objectContaining({ method: 'POST' }))
  })
  it('keeps the dialog open and shows duplicate-report conflict', async () => {
    vi.mocked(api).mockRejectedValueOnce(new Error('Báo cáo trước đang chờ xử lý.'))
    const close = vi.fn()
    const user = userEvent.setup()
    render(<ReportDialog postId="42" onClose={close} />)
    await user.click(screen.getByRole('button', { name: 'Gửi báo cáo' }))
    expect(screen.getByRole('alert')).toHaveTextContent('Báo cáo trước đang chờ xử lý.')
    expect(close).not.toHaveBeenCalled()
  })
})
