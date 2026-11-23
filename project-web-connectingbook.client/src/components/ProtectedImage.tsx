import { useEffect, useState } from 'react'
import { mediaBlob } from '../api/client'

export function ProtectedImage({ id, alt, className = '', path }: { id: string; alt: string; className?: string; path?: string }) {
  const [url, setUrl] = useState('')
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    let objectUrl = ''
    mediaBlob(id, controller.signal, path).then(value => {
      objectUrl = value
      if (!controller.signal.aborted) setUrl(value)
      else URL.revokeObjectURL(value)
    }).catch(() => { if (!controller.signal.aborted) setFailed(true) })
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl) }
  }, [id, path])
  return url ? <img src={url} alt={alt} className={className} /> : <span className={`media-placeholder ${className}`}>{failed ? 'Không tải được ảnh' : 'Đang tải ảnh…'}</span>
}

export function Avatar({ id, name }: { id: string | null; name: string }) {
  return id ? <ProtectedImage id={id} alt={`Ảnh đại diện của ${name}`} className="avatar" /> : <span className="avatar initials" aria-label={name}>{name.trim().slice(0, 1).toUpperCase()}</span>
}
