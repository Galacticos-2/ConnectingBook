import { useEffect, useState } from 'react'
import { message, restoreSession } from '../api/client'
import { AuthContext } from './context'
import type { ReactNode } from 'react'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    restoreSession().catch(error => { if (active) setError(message(error)) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])
  return <AuthContext.Provider value={{ loading, error }}>{children}</AuthContext.Provider>
}
